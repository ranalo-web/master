using Microsoft.AspNetCore.Mvc;
using Ranalo.Services;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;
using System.Globalization;

namespace Ranalo.Controllers
{
    // M-Pesa payments forwarded from IONOS (Safaricom C2B fields), the live way
    // payments arrive. Checks and Monitor/Enforce mode in Services/MpesaIntakeRules.cs.
    [ApiController]
    [Route("api/mpesa")]
    public class MpesaController : ControllerBase
    {
        private readonly ISyncService _syncService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MpesaController> _logger;

        public MpesaController(ISyncService syncService, IConfiguration configuration, ILogger<MpesaController> logger)
        {
            _syncService = syncService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("transaction")]
        [RequestSizeLimit(64 * 1024)]
        public async Task<IActionResult> ReceiveTransaction([FromQuery] string? key)
        {
            // Read the body by hand: binding to MpesaTransaction refused a
            // payment with no FirstName (or a numeric amount, or a non-JSON
            // content type) with a 400 before this ran, so it was never logged.
            string body;
            using (var reader = new StreamReader(Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }
            var transaction = MpesaIntakeRules.Read(body);

            var enforce = MpesaIntakeRules.Enforce(_configuration["MpesaIntake:Mode"]);
            var ip = MpesaIntakeRules.SenderIp(Request.Headers["X-Forwarded-For"].ToString(), HttpContext.Connection.RemoteIpAddress?.ToString());
            var transId = transaction?.TransID?.Trim();

            _logger.LogInformation("M-Pesa payment received from {Ip}: TransID {TransID}, account {Account}, amount {Amount}, mode {Mode}",
                ip, transId, transaction?.BillRefNumber, transaction?.TransAmount, enforce ? "Enforce" : "Monitor");

            if (transaction == null)
            {
                _logger.LogWarning("M-Pesa payment refused: body unreadable, from {Ip}, {Length} chars", ip, body.Length);
                return BadRequest("Invalid transaction data.");
            }

            var provided = Request.Headers.TryGetValue(MpesaIntakeRules.SecretHeader, out var header) ? header.ToString() : key;
            if (!MpesaIntakeRules.SecretOk(_configuration["MpesaIntake:SharedSecret"], provided))
            {
                if (enforce)
                {
                    _logger.LogWarning("M-Pesa payment refused: wrong or missing key, from {Ip}, TransID {TransID}", ip, transId);
                    return Unauthorized();
                }
                _logger.LogWarning("M-Pesa monitor: would refuse TransID {TransID}, wrong or missing key ({KeySent}), from {Ip}",
                    transId, string.IsNullOrEmpty(provided) ? "none sent" : "sent", ip);
            }

            if (!MpesaIntakeRules.IpAllowed(_configuration["MpesaIntake:AllowedIps"], ip))
            {
                if (enforce)
                {
                    _logger.LogWarning("M-Pesa payment refused: IP {Ip} not allowed, TransID {TransID}", ip, transId);
                    return Unauthorized();
                }
                _logger.LogWarning("M-Pesa monitor: would refuse TransID {TransID}, IP {Ip} not allowed", transId, ip);
            }

            var invalid = MpesaIntakeRules.Invalid(transId, transaction.TransAmount, transaction.BillRefNumber, transaction.TransTime);
            if (invalid != null)
            {
                if (enforce)
                {
                    _logger.LogWarning("M-Pesa payment refused: {Reason} TransID {TransID}", invalid, transId);
                    return BadRequest(invalid);
                }
                _logger.LogWarning("M-Pesa monitor: would refuse TransID {TransID}: {Reason}", transId, invalid);
            }

            if (!string.IsNullOrEmpty(transId) && await _syncService.MpesaCodeExistsAsync(transId))
            {
                if (enforce)
                {
                    // Still 200 so the sender doesn't keep retrying.
                    _logger.LogWarning("M-Pesa payment {TransID} already recorded; not added again", transId);
                    return Ok(new { Message = $"Transaction {transId} already recorded" });
                }
                _logger.LogWarning("M-Pesa monitor: TransID {TransID} already recorded, would not add again", transId);
            }

            int result;
            try
            {
                var newPayment = new MpesaRecord()
                {
                    AccountNo = transaction.BillRefNumber?.Trim()!,
                    Amount = transaction.TransAmount!,
                    MpesaCode = transId!,
                    PaymentDate = DateTime.ParseExact(transaction.TransTime!, MpesaIntakeRules.TransTimeFormat, CultureInfo.InvariantCulture).ToString(),
                    FirstName = transaction.FirstName
                };

                result = await _syncService.CreateKosePaymentAsync(newPayment);
            }
            catch (Exception ex)
            {
                // As before these fail with a 500 (so the sender retries), but now logged.
                _logger.LogError(ex, "M-Pesa payment {TransID} could not be saved", transId);
                throw;
            }

            _logger.LogInformation("M-Pesa payment {TransID} saved as {Id}: account {Account}, amount {Amount}",
                transId, result, transaction.BillRefNumber, transaction.TransAmount);

            return Ok(new { Message = $"Transaction received successfully {result}", Received = transaction });
        }
    }

    public class MpesaTransaction
    {
        public string? TransID { get; set; }
        public string? TransAmount { get; set; }
        public string? BillRefNumber { get; set; }
        public string? TransTime { get; set; }
        public string? FirstName { get; set; }
    }
}
