using Microsoft.AspNetCore.Mvc;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly IPaymentsService _paymentsService;
        public PaymentsController(IPaymentsService paymentsService )
        {
            _paymentsService = paymentsService;
        }

        [HttpPost("upload-payments")]
        public async Task<IActionResult> UploadStatement(IFormFile file)
        {
            try
            {
                var payments = RanaloXlsmUploadParser.Parse(file);

                if (payments.Any())
                {
                    var mapped = _paymentsService.MapXlsPayments(payments);
                    var results = await _paymentsService.CreatePayments(mapped);
                }
            }
            catch (Exception)
            {

                return RedirectToAction("AllPayments", "Home");
            }
            

            //return Ok(new
            //{
            //    Count = payments.Count,
            //    TotalPaidIn = payments.Sum(x => x.PaidIn),
            //    Payments = payments
            //});

            return RedirectToAction("AllPayments", "Home");
        }
    }
}
