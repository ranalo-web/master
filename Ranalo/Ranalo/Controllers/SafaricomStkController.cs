using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Controllers
{
    // Safaricom's answer to an M-Pesa Express (STK push) prompt sent by
    // MpesaStkClient: paid, cancelled, timed out, wrong PIN...
    //   CallBackURL https://<site>/api/kose/stk/<SafaricomStk:CallbackToken>/callback
    // A paid one is saved to KosePayments by its M-Pesa code, exactly as a
    // paybill payment (SafaricomC2BController), so if Safaricom also sends
    // the C2B confirmation the payment still counts once.
    //
    // Server-to-server, no login. Trusted like the C2B callbacks: the secret
    // token in the URL and Safaricom's IPs (SafaricomC2B:AllowedIps).
    [ApiController]
    public class SafaricomStkController : ControllerBase
    {
        private const int MaxBodyBytes = 16 * 1024;

        private readonly IStkRequestRepository _requests;
        private readonly IKosePaymentsRepository _payments;
        private readonly IAccountRefreshQueue _refresh;
        private readonly SafaricomStkSettings _stk;
        private readonly SafaricomC2BSettings _c2b;
        private readonly ILogger<SafaricomStkController> _logger;

        public SafaricomStkController(IStkRequestRepository requests, IKosePaymentsRepository payments, IAccountRefreshQueue refresh,
            IOptions<SafaricomStkSettings> stk, IOptions<SafaricomC2BSettings> c2b, ILogger<SafaricomStkController> logger)
        {
            _requests = requests;
            _payments = payments;
            _refresh = refresh;
            _stk = stk.Value;
            _c2b = c2b.Value;
            _logger = logger;
        }

        [HttpPost("api/kose/stk/{token}/callback")]
        [RequestSizeLimit(MaxBodyBytes)]
        public async Task<IActionResult> Callback(string token)
        {
            var caller = SafaricomCallerRules.CallerIp(HttpContext.Connection.RemoteIpAddress, Request.Headers["X-Forwarded-For"].ToString());
            if (!SafaricomCallerRules.TokenOk(_stk.CallbackToken, token))
            {
                _logger.LogWarning("STK callback from {Caller} refused: wrong or unset SafaricomStk__CallbackToken.", caller);
                return NotFound();
            }
            if (_c2b.EnforceIpAllowlist && !SafaricomCallerRules.IpAllowed(caller, _c2b.AllowedIps))
            {
                _logger.LogWarning("STK callback from {Caller} refused: not a Safaricom IP.", caller);
                return StatusCode(403);
            }

            StkRules.Result? result;
            try
            {
                using var doc = await JsonDocument.ParseAsync(Request.Body);
                result = StkRules.ReadCallback(doc.RootElement);
            }
            catch (JsonException)
            {
                result = null;
            }
            if (result == null)
            {
                _logger.LogError("STK callback: body not understood, nothing saved.");
                return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
            }

            try
            {
                var request = await _requests.GetAsync(result.CheckoutRequestId);
                if (request == null)
                {
                    // Everything needed to add it by hand is in this line.
                    _logger.LogError("STK callback for unknown Checkout={Checkout}: Code={Code} Receipt={Receipt} Amount={Amount} Phone={Phone} PaidAt={PaidAt}",
                        result.CheckoutRequestId, result.ResultCode, result.Receipt, result.Amount, result.Phone, result.PaidAtKenya);
                    return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
                }

                if (!result.Paid)
                {
                    await _requests.CompleteAsync(result.CheckoutRequestId, StkStatus.Failed, result.ResultCode, result.ResultDesc, null);
                    _logger.LogInformation("STK not paid: Account={Account} Code={Code} {Desc}", request.AccountNo, result.ResultCode, result.ResultDesc);
                    return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
                }

                var record = new MpesaRecord
                {
                    AccountNo = request.AccountNo.ToString(CultureInfo.InvariantCulture),
                    MpesaCode = result.Receipt!,
                    Amount = result.Amount!.Value.ToString(CultureInfo.InvariantCulture),
                    // Nairobi time, as KosePayments holds it.
                    PaymentDate = (result.PaidAtKenya ?? DateTime.UtcNow.AddHours(3)).ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                };
                var added = await _payments.InsertIfNewAsync(record);
                await _requests.CompleteAsync(result.CheckoutRequestId, StkStatus.Paid, result.ResultCode, result.ResultDesc, result.Receipt);

                _logger.LogInformation(added
                        ? "STK payment saved: Receipt={Receipt} Amount={Amount} Account={Account}"
                        : "STK payment already recorded: Receipt={Receipt} Amount={Amount} Account={Account}",
                    record.MpesaCode, record.Amount, record.AccountNo);

                if (added)
                {
                    // Contract and lock date now, not at the next scheduled run.
                    _refresh.Enqueue(record.AccountNo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "STK callback NOT saved (database error): Checkout={Checkout} Receipt={Receipt} Amount={Amount} PaidAt={PaidAt}",
                    result.CheckoutRequestId, result.Receipt, result.Amount, result.PaidAtKenya);
                return StatusCode(500, new { ResultCode = 1, ResultDesc = "Server error" });
            }

            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }
    }
}
