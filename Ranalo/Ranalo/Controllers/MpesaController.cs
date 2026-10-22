using Microsoft.AspNetCore.Mvc;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;
using System.Globalization;

namespace Ranalo.Controllers
{
    // Payments forwarded from IONOS (malipo/confirmpayments.php). Closed on
    // 2026-10-22: it had no checks, so anyone could add a payment. Safaricom
    // now posts straight to SafaricomC2BController.
    //
    // Emergency only: Azure setting MpesaIntake__LegacyEndpointOpen = true
    // opens it again without a redeploy.
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
        public async Task<IActionResult> ReceiveTransaction([FromBody] MpesaTransaction transaction)
        {
            if (!_configuration.GetValue<bool>("MpesaIntake:LegacyEndpointOpen"))
            {
                // Logged so a real payment that still arrives here can be
                // added by hand (or from the statement upload).
                _logger.LogWarning(
                    "Closed api/mpesa/transaction refused: TransID={TransID} Amount={Amount} Account={Account} TransTime={TransTime} FirstName={FirstName}",
                    transaction?.TransID, transaction?.TransAmount, transaction?.BillRefNumber, transaction?.TransTime, transaction?.FirstName);
                return NotFound();
            }

            if (transaction == null)
                return BadRequest("Invalid transaction data.");

            // TODO: Save to database or trigger business logic
            var newPayment = new MpesaRecord()
            {
                AccountNo = transaction.BillRefNumber,
                Amount = transaction.TransAmount,
                MpesaCode = transaction.TransID,
                PaymentDate = DateTime.ParseExact(transaction.TransTime, "yyyyMMddHHmmss", CultureInfo.InvariantCulture).ToString(),
                FirstName = transaction.FirstName
            };

            var result = await _syncService.CreateKosePaymentAsync(newPayment);

            // Return success response
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
