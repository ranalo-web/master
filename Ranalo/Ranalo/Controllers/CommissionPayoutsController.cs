using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Pay Commissions (admin): see what each dealer and agent is owed after
    // arrears and suspension, pay one account or a consolidated batch, and
    // record the payment details. Payment History is shared: admins see
    // every payout, dealers and agents see their own and can confirm receipt.
    [LoadUserSettingsFromCookie]
    public class CommissionPayoutsController : Controller
    {
        private readonly ICommissionPayoutService _payoutService;

        public CommissionPayoutsController(ICommissionPayoutService payoutService)
        {
            _payoutService = payoutService;
        }

        [HttpGet]
        [Route("commission-payouts")]
        public async Task<IActionResult> Index(string? type = null, string? q = null)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            SetViewBags(settings!);
            return View(await _payoutService.GetPayeesAsync(type, q));
        }

        // select: contract ids to open with ticked (from a search match).
        [HttpGet]
        [Route("commission-payouts/pay")]
        public async Task<IActionResult> Pay(string type, int id, List<long>? select = null)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            var payeeType = CommissionPayeeType.Normalize(type);
            var model = payeeType == null ? null : await _payoutService.GetPayAsync(payeeType, id);
            if (model == null)
            {
                TempData["PayoutError"] = "That dealer or agent has no commission accounts.";
                return RedirectToAction("Index");
            }

            model.PreselectedContractIds = select ?? new List<long>();
            SetViewBags(settings!);
            return View(model);
        }

        [HttpPost]
        [Route("commission-payouts/pay")]
        public async Task<IActionResult> Pay(string type, int id, List<long> contractIds, decimal amount, bool recordPastPayment,
            DateTime paidDate, string method, string? reference, string? notes)
        {
            if (!TryGetAdmin(out var settings, out var redirect))
            {
                return redirect!;
            }

            var payeeType = CommissionPayeeType.Normalize(type);
            if (payeeType == null)
            {
                return RedirectToAction("Index");
            }

            var (ok, message) = await _payoutService.RecordPayoutAsync(
                payeeType, id, contractIds ?? new List<long>(), amount, recordPastPayment, paidDate, method, reference, notes, settings!.UserId);

            TempData[ok ? "PayoutSuccess" : "PayoutError"] = message;
            return RedirectToAction("Pay", new { type = payeeType, id });
        }

        [HttpGet]
        [Route("commission-payouts/history")]
        public async Task<IActionResult> History()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            List<CommissionPayoutRecord> payouts;
            switch (settings.RoleId)
            {
                case UserRole.Admin:
                    payouts = await _payoutService.GetPayoutsAsync();
                    break;
                case UserRole.Dealer:
                    payouts = await _payoutService.GetPayoutsAsync(CommissionPayeeType.Dealer, dealerId: settings.DealerId);
                    break;
                case UserRole.Agent:
                    payouts = await _payoutService.GetPayoutsAsync(CommissionPayeeType.Agent, agentUserId: settings.UserId);
                    break;
                default:
                    return RedirectToAction("Index", "Home");
            }

            SetViewBags(settings);
            return View(new CommissionPayoutHistoryViewModel
            {
                IsAdminView = settings.RoleId == UserRole.Admin,
                Payouts = payouts,
            });
        }

        // The payee confirms they received a payout. Optional -- nothing
        // else depends on it.
        [HttpPost]
        [Route("commission-payouts/{id:int}/confirm")]
        public async Task<IActionResult> ConfirmReceipt(int id)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var confirmed = settings.RoleId switch
            {
                UserRole.Dealer => await _payoutService.ConfirmReceiptAsync(id, CommissionPayeeType.Dealer, settings.DealerId, settings.UserId),
                UserRole.Agent => await _payoutService.ConfirmReceiptAsync(id, CommissionPayeeType.Agent, settings.UserId, settings.UserId),
                _ => false,
            };

            TempData[confirmed ? "PayoutSuccess" : "PayoutError"] = confirmed
                ? "Thanks -- receipt confirmed."
                : "That payment couldn't be confirmed (it may already be confirmed).";
            return RedirectToAction("History");
        }

        private bool TryGetAdmin(out User? settings, out IActionResult? redirect)
        {
            settings = HttpContext.Items["UserSettings"] as User;
            redirect = settings == null
                ? RedirectToAction("Index", "Login")
                : settings.RoleId != UserRole.Admin ? RedirectToAction("Index", "Home") : null;
            return redirect == null;
        }

        private void SetViewBags(User settings)
        {
            ViewBag.BackLink = "commission-payouts";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.IsAgent = settings.RoleId == UserRole.Agent;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
