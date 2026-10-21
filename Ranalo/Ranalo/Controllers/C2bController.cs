using Microsoft.AspNetCore.Mvc;
using Ranalo.Services;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;
using System.Globalization;

namespace Ranalo.Controllers
{
    // Safaricom C2B callbacks received directly, rules in Services/C2bRules.cs.
    // Register in Daraja URL Management as:
    //   Confirmation: https://<site>/api/malipo/confirm
    //   Validation:   https://<site>/api/malipo/validate
    // (Daraja refuses URLs containing "mpesa", hence not api/mpesa/...)
    [ApiController]
    [Route("api/malipo")]
    public class C2bController : ControllerBase
    {
        private readonly ISyncService _syncService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<C2bController> _logger;

        public C2bController(ISyncService syncService, IConfiguration configuration, ILogger<C2bController> logger)
        {
            _syncService = syncService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("confirm")]
        [RequestSizeLimit(64 * 1024)]
        public async Task<IActionResult> Confirm()
        {
            var body = await ReadBodyAsync();
            var ip = SenderIp();

            if (!MpesaIntakeRules.IpAllowed(C2bRules.AllowedIps(_configuration["C2b:AllowedIps"]), ip))
            {
                _logger.LogWarning("C2B confirmation refused: IP {Ip} is not Safaricom. Body: {Body}", ip, body);
                return Unauthorized();
            }

            var transaction = MpesaIntakeRules.Read(body);
            var transId = transaction?.TransID?.Trim();
            var invalid = transaction == null
                ? "Invalid transaction data."
                : MpesaIntakeRules.Invalid(transId, transaction.TransAmount, transaction.BillRefNumber, transaction.TransTime);
            if (invalid != null)
            {
                // Still accepted so Safaricom doesn't retry something that will never
                // save; the body is logged so the payment can be entered by hand.
                _logger.LogError("C2B confirmation not saved: {Reason} Body: {Body}", invalid, body);
                return Ok(C2bRules.Accepted);
            }

            var payment = new MpesaRecord()
            {
                AccountNo = transaction!.BillRefNumber!.Trim(),
                Amount = transaction.TransAmount!,
                MpesaCode = transId!,
                PaymentDate = DateTime.ParseExact(transaction.TransTime!, MpesaIntakeRules.TransTimeFormat, CultureInfo.InvariantCulture).ToString(),
                FirstName = transaction.FirstName
            };

            int id;
            try
            {
                id = await _syncService.CreateKosePaymentIfNewAsync(payment);
            }
            catch (Exception ex)
            {
                // A 500 makes Safaricom retry later.
                _logger.LogError(ex, "C2B payment {TransID} could not be saved. Body: {Body}", transId, body);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            if (id == 0)
            {
                _logger.LogInformation("C2B payment {TransID} already recorded; not added again", transId);
            }
            else
            {
                _logger.LogInformation("C2B payment {TransID} saved as {Id}: account {Account}, amount {Amount}",
                    transId, id, payment.AccountNo, payment.Amount);
            }
            return Ok(C2bRules.Accepted);
        }

        // Only called if Safaricom has external validation switched on for the
        // paybill; every payment is accepted, as IONOS's validation_url.php does.
        [HttpPost("validate")]
        [RequestSizeLimit(64 * 1024)]
        public async Task<IActionResult> Validate()
        {
            var body = await ReadBodyAsync();
            var ip = SenderIp();

            if (!MpesaIntakeRules.IpAllowed(C2bRules.AllowedIps(_configuration["C2b:AllowedIps"]), ip))
            {
                _logger.LogWarning("C2B validation refused: IP {Ip} is not Safaricom", ip);
                return Unauthorized();
            }

            _logger.LogInformation("C2B validation for TransID {TransID}", MpesaIntakeRules.Read(body)?.TransID);
            return Ok(C2bRules.Accepted);
        }

        private async Task<string> ReadBodyAsync()
        {
            using var reader = new StreamReader(Request.Body);
            return await reader.ReadToEndAsync();
        }

        private string? SenderIp() =>
            MpesaIntakeRules.SenderIp(Request.Headers["X-Forwarded-For"].ToString(), HttpContext.Connection.RemoteIpAddress?.ToString());
    }
}
