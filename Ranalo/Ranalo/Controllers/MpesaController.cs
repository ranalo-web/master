using Microsoft.AspNetCore.Mvc;
using Ranalo.Services;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;
using System.Globalization;

namespace Ranalo.Controllers
{
    // M-Pesa payments forwarded from IONOS (Safaricom C2B fields). The live
    // way payments arrive -- checks in Services/MpesaIntakeRules.cs.
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
        public async Task<IActionResult> ReceiveTransaction([FromBody] MpesaTransaction transaction, [FromQuery] string? key)
        {
            var provided = Request.Headers.TryGetValue(MpesaIntakeRules.SecretHeader, out var header) ? header.ToString() : key;
            if (!MpesaIntakeRules.SecretOk(_configuration["MpesaIntake:SharedSecret"], provided))
            {
                _logger.LogWarning("M-Pesa payment refused: wrong or missing key, from {Ip}, TransID {TransID}",
                    HttpContext.Connection.RemoteIpAddress, transaction?.TransID);
                return Unauthorized();
            }

            var invalid = transaction == null
                ? "Invalid transaction data."
                : MpesaIntakeRules.Invalid(transaction.TransID, transaction.TransAmount, transaction.BillRefNumber, transaction.TransTime);
            if (invalid != null)
            {
                _logger.LogWarning("M-Pesa payment refused: {Reason} TransID {TransID}", invalid, transaction?.TransID);
                return BadRequest(invalid);
            }

            var newPayment = new MpesaRecord()
            {
                AccountNo = transaction!.BillRefNumber.Trim(),
                Amount = transaction.TransAmount,
                MpesaCode = transaction.TransID.Trim(),
                PaymentDate = DateTime.ParseExact(transaction.TransTime, MpesaIntakeRules.TransTimeFormat, CultureInfo.InvariantCulture).ToString(),
                FirstName = transaction.FirstName
            };

            // 0 = this M-Pesa code is already recorded; still 200 so the sender doesn't keep retrying.
            var result = await _syncService.CreateKosePaymentAsync(newPayment);
            if (result == 0)
            {
                _logger.LogWarning("M-Pesa payment {TransID} already recorded; not added again", newPayment.MpesaCode);
                return Ok(new { Message = $"Transaction {newPayment.MpesaCode} already recorded" });
            }

            return Ok(new { Message = $"Transaction received successfully {result}", Received = transaction });
        }
    }

    public class MpesaTransaction
    {
        public string TransID { get; set; }
        public string TransAmount { get; set; }
        public string BillRefNumber { get; set; }
        public string TransTime { get; set; }
        public string FirstName { get; set; }
    }
}
