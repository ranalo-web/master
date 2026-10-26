using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;
using Ranalo.Services.DeviceLock;

namespace Ranalo.Controllers
{
    // See DepositSafeguards.
    [LoadUserSettingsFromCookie]
    public class DepositSafeguardsController : Controller
    {
        // How far back the enrolment page looks for the deposit.
        private static readonly TimeSpan PaymentsLookBack = TimeSpan.FromDays(30);
        // Older approvals than this are history, not something to chase.
        private static readonly TimeSpan ApprovalsLookBack = TimeSpan.FromDays(60);

        private readonly IDepositSafeguardsRepository _repository;

        public DepositSafeguardsController(IDepositSafeguardsRepository repository)
        {
            _repository = repository;
        }

        // The enrolment page's deposit picker: payments on the customer's
        // account that can be this order's deposit. Code, amount and time
        // only -- the dealer has typed the customer's ID number already.
        [HttpGet]
        [Route("enrolments/deposit-choices")]
        public async Task<IActionResult> DepositChoices(long accountId, long orderId)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null || !DeviceLockRules.CanEnrol(settings.RoleId))
            {
                return Unauthorized();
            }

            if (accountId <= 0)
            {
                return Json(Array.Empty<object>());
            }

            var payments = await _repository.GetAccountPaymentsAsync(accountId, DateTime.Now - PaymentsLookBack);
            return Json(DepositSafeguards.DepositChoices(payments, orderId).Select(p => new
            {
                code = p.MpesaCode,
                amount = p.Amount,
                paidAt = p.PaidAt.ToString("dd/MM/yyyy h:mm tt")
            }));
        }

        [HttpGet]
        [Route("reports/approved-no-contract")]
        public async Task<IActionResult> ApprovedWithoutContract()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId is not (UserRole.Admin or UserRole.Approver))
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = false;
            ViewBag.IsAgent = false;
            ViewBag.UserName = settings.KnownAs;

            // ApprovedDate is UTC (DateTime.UtcNow when approved).
            var now = DateTime.UtcNow;
            var rows = await _repository.GetApprovedWithoutContractAsync(now - DepositSafeguards.ContractGrace, now - ApprovalsLookBack);
            foreach (var row in rows)
            {
                row.RecentPayments = await _repository.GetAccountPaymentsAsync(row.AccountId, DateTime.Now - PaymentsLookBack);
            }

            return View("~/Views/Reports/ApprovedWithoutContract.cshtml", rows);
        }
    }
}
