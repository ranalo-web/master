using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Models;
using System.Globalization;
using System.Text.Json;

namespace Ranalo.Controllers
{
    // Safaricom Daraja C2B callbacks for our paybill, received directly
    // instead of via IONOS (malipo/confirmpayments.php) -- each payment goes
    // straight into KosePayments.
    //
    // Register these with Safaricom (C2B Register URL, ResponseType
    // "Completed"):
    //   ConfirmationURL https://<site>/api/kose/c2b/<CallbackToken>/confirmation
    //   ValidationURL   https://<site>/api/kose/c2b/<CallbackToken>/validation
    // Safaricom refuses URLs containing words like "mpesa" or "safaricom",
    // hence "kose/c2b".
    //
    // Server-to-server, no login. See SafaricomCallerRules for how a caller
    // is trusted. A wrong token gets a plain 404 so the endpoint can't be
    // found by guessing.
    [ApiController]
    public class SafaricomC2BController : ControllerBase
    {
        private const int MaxBodyBytes = 16 * 1024;

        private readonly IKosePaymentsRepository _payments;
        private readonly IAccountRefreshQueue _refresh;
        private readonly SafaricomC2BSettings _settings;
        private readonly ILogger<SafaricomC2BController> _logger;

        public SafaricomC2BController(
            IKosePaymentsRepository payments,
            IAccountRefreshQueue refresh,
            IOptions<SafaricomC2BSettings> options,
            ILogger<SafaricomC2BController> logger)
        {
            _payments = payments;
            _refresh = refresh;
            _settings = options.Value;
            _logger = logger;
        }

        // Called after the money has moved: record the payment.
        [HttpPost("api/kose/c2b/{token}/confirmation")]
        [RequestSizeLimit(MaxBodyBytes)]
        public async Task<IActionResult> Confirmation(string token)
        {
            var refused = RefuseCaller(token, "confirmation");
            if (refused != null)
            {
                return refused;
            }

            var body = await ReadBodyAsync();
            var transaction = body.HasValue ? MpesaIntakeRules.Read(body.Value) : null;
            if (transaction == null)
            {
                _logger.LogError("C2B confirmation: body isn't a JSON object, nothing saved.");
                return BadRequest(new { ResultCode = 1, ResultDesc = "Invalid request" });
            }

            var shortCode = MpesaIntakeRules.Field(body!.Value, "BusinessShortCode");
            var invalid = WrongShortCode(shortCode)
                ?? MpesaIntakeRules.Invalid(transaction.TransID, transaction.TransAmount, transaction.BillRefNumber, transaction.TransTime);

            if (invalid != null)
            {
                // Everything needed to add it by hand is in this line.
                _logger.LogError(
                    "C2B confirmation refused ({Reason}): TransID={TransID} Amount={Amount} Account={Account} TransTime={TransTime} ShortCode={ShortCode} FirstName={FirstName}",
                    invalid, transaction.TransID, transaction.TransAmount, transaction.BillRefNumber, transaction.TransTime, shortCode, transaction.FirstName);
                return BadRequest(new { ResultCode = 1, ResultDesc = "Invalid request" });
            }

            var record = new MpesaRecord
            {
                AccountNo = transaction.BillRefNumber!.Trim(),
                MpesaCode = transaction.TransID!.Trim().ToUpperInvariant(),
                Amount = decimal.Parse(transaction.TransAmount!, NumberStyles.Number, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                // TransTime is Nairobi time, which is what KosePayments holds.
                // Same text as the rest of the table ("10/21/2026 9:09:01 PM"):
                // reports pick first/last payment by this text.
                PaymentDate = DateTime.ParseExact(transaction.TransTime!, MpesaIntakeRules.TransTimeFormat, CultureInfo.InvariantCulture)
                    .ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                FirstName = transaction.FirstName?.Trim()
            };

            try
            {
                // Safaricom can send the same payment more than once.
                var added = await _payments.InsertIfNewAsync(record);

                _logger.LogInformation(
                    added
                        ? "C2B confirmation saved: TransID={TransID} Amount={Amount} Account={Account}"
                        : "C2B confirmation already recorded, skipped: TransID={TransID} Amount={Amount} Account={Account}",
                    record.MpesaCode, record.Amount, record.AccountNo);

                if (added)
                {
                    await SaveDetailsAsync(body.Value, record.MpesaCode);

                    // Contract and lock date now, not at the next scheduled run.
                    _refresh.Enqueue(record.AccountNo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "C2B confirmation NOT saved (database error): TransID={TransID} Amount={Amount} Account={Account} TransTime={TransTime} FirstName={FirstName}",
                    record.MpesaCode, record.Amount, record.AccountNo, transaction.TransTime, record.FirstName);
                return StatusCode(500, new { ResultCode = 1, ResultDesc = "Server error" });
            }

            return Ok(new { ResultCode = 0, ResultDesc = "Confirmation Received Successfully" });
        }

        // Called before the money moves, only if Safaricom has external
        // validation switched on for the paybill. Nothing is saved here. We
        // accept everything except our own obvious errors -- refusing an
        // account number would stop the customer paying at all.
        [HttpPost("api/kose/c2b/{token}/validation")]
        [RequestSizeLimit(MaxBodyBytes)]
        public async Task<IActionResult> Validation(string token)
        {
            var refused = RefuseCaller(token, "validation");
            if (refused != null)
            {
                return refused;
            }

            var body = await ReadBodyAsync();
            var transaction = body.HasValue ? MpesaIntakeRules.Read(body.Value) : null;

            if (transaction == null)
            {
                return Ok(new { ResultCode = "C2B00016", ResultDesc = "Rejected" });
            }
            if (WrongShortCode(MpesaIntakeRules.Field(body!.Value, "BusinessShortCode")) != null)
            {
                return Ok(new { ResultCode = "C2B00015", ResultDesc = "Rejected" });
            }
            if (!decimal.TryParse(transaction.TransAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                return Ok(new { ResultCode = "C2B00013", ResultDesc = "Rejected" });
            }

            return Ok(new { ResultCode = "0", ResultDesc = "Accepted" });
        }

        private IActionResult? RefuseCaller(string token, string callback)
        {
            var caller = SafaricomCallerRules.CallerIp(HttpContext.Connection.RemoteIpAddress, Request.Headers["X-Forwarded-For"].ToString());

            if (!SafaricomCallerRules.TokenOk(_settings.CallbackToken, token))
            {
                _logger.LogWarning(
                    string.IsNullOrEmpty(_settings.CallbackToken)
                        ? "C2B {Callback} from {Caller} refused: SafaricomC2B__CallbackToken isn't set in Azure."
                        : "C2B {Callback} from {Caller} refused: wrong token.",
                    callback, caller);
                return NotFound();
            }

            if (_settings.EnforceIpAllowlist && !SafaricomCallerRules.IpAllowed(caller, _settings.AllowedIps))
            {
                _logger.LogWarning("C2B {Callback} from {Caller} refused: not a Safaricom IP (SafaricomC2B:AllowedIps).", callback, caller);
                return StatusCode(403);
            }

            return null;
        }

        // The rest of the callback, onto the row just saved. The payment
        // itself is already in, so a failure here (e.g. the columns from
        // Database/Payments/002 not added yet) is only logged.
        private async Task SaveDetailsAsync(JsonElement body, string mpesaCode)
        {
            // Clipped to the column sizes so an odd value can't fail the update.
            string? Text(string name, int max)
            {
                var value = MpesaIntakeRules.Field(body, name)?.Trim();
                return string.IsNullOrEmpty(value) ? null : value.Length > max ? value[..max] : value;
            }

            try
            {
                await _payments.SaveC2BDetailsAsync(new C2BPaymentDetails
                {
                    MpesaCode = mpesaCode,
                    MiddleName = Text("MiddleName", 100),
                    LastName = Text("LastName", 100),
                    Msisdn = Text("MSISDN", 100),
                    TransactionType = Text("TransactionType", 50),
                    BusinessShortCode = Text("BusinessShortCode", 20),
                    InvoiceNumber = Text("InvoiceNumber", 100),
                    OrgAccountBalance = Text("OrgAccountBalance", 30),
                    ThirdPartyTransID = Text("ThirdPartyTransID", 100)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "C2B confirmation {TransID}: payment saved, extra details skipped.", mpesaCode);
            }
        }

        private string? WrongShortCode(string? shortCode)
        {
            if (string.IsNullOrEmpty(_settings.ShortCode) || string.Equals(shortCode?.Trim(), _settings.ShortCode, StringComparison.Ordinal))
            {
                return null;
            }

            return "Wrong BusinessShortCode.";
        }

        private async Task<JsonElement?> ReadBodyAsync()
        {
            try
            {
                using var document = await JsonDocument.ParseAsync(Request.Body);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
