using Microsoft.AspNetCore.Mvc;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;
using System.Globalization;

namespace Ranalo.Controllers
{
    [ApiController]
    [Route("api/mpesa")]
    public class MpesaController : ControllerBase
    {
        private readonly ISyncService _syncService;

        public MpesaController(ISyncService syncService)
        {
            _syncService = syncService;
        }

        [HttpPost("transaction")]
        public async Task<IActionResult> ReceiveTransaction([FromBody] MpesaTransaction transaction)
        {
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
