using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;
using Ranalo.Services.DeviceLock;

namespace Ranalo.Controllers
{
    // Stock phones: lock Knox/Transsion phones by IMEI with no order, so they
    // go to the field locked (Services/DeviceLock/StockPhoneRules.cs). Admins
    // and approvers see all; dealers their own. Selling one is the normal Add Enrolment with the order.
    [LoadUserSettingsFromCookie]
    public class StockPhonesController : Controller
    {
        private readonly IStockPhoneService _stock;
        private readonly IEnrolmentRepository _enrolments;
        private readonly IUserService _users;

        public StockPhonesController(IStockPhoneService stock, IEnrolmentRepository enrolments, IUserService users)
        {
            _stock = stock;
            _enrolments = enrolments;
            _users = users;
        }

        [HttpGet]
        [Route("enrolments/stock")]
        public async Task<IActionResult> Index(string filter = StockPhoneRules.FilterAll, string? q = null)
        {
            if (!CanManage(out var redirect))
            {
                return redirect!;
            }

            var all = (await _enrolments.GetStockAsync(false, q?.Trim()))
                .Where(e => StockPhoneRules.CanSee(CurrentUser, e)).ToList();
            filter = filter == StockPhoneRules.FilterUnverified ? filter : StockPhoneRules.FilterAll;
            var model = new StockPhonesViewModel
            {
                Filter = filter,
                Search = q?.Trim(),
                Total = all.Count,
                Unverified = all.Count(StockPhoneRules.NeedsVerifying),
                Rows = filter == StockPhoneRules.FilterUnverified ? all.Where(StockPhoneRules.NeedsVerifying).ToList() : all,
                Dealers = (await _users.GetAllDealers() ?? new List<DataStore.Dealer>())
                    .Where(d => StockPhoneRules.SeesAll(CurrentUser.RoleId) || d.DealerId == CurrentUser.DealerId)
                    .OrderBy(d => d.CompanyName).ToDictionary(d => d.DealerId, d => d.CompanyName),
                ChooseDealer = StockPhoneRules.SeesAll(CurrentUser.RoleId),
            };
            if (TempData["StockMessages"] is string[] messages) model.Messages.AddRange(messages);
            if (TempData["StockErrors"] is string[] errors) model.Errors.AddRange(errors);

            SetLayoutViewBags();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/stock/lock")]
        public async Task<IActionResult> Lock(string? imeis, string? brand, int dealerId = 0)
        {
            if (!CanManage(out var redirect))
            {
                return redirect!;
            }

            var (messages, errors) = await _stock.LockAsync(imeis, brand, StockPhoneRules.DealerFor(CurrentUser, dealerId), CurrentUser);
            TempData["StockMessages"] = messages.ToArray();
            TempData["StockErrors"] = errors.ToArray();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/stock/{id:guid}/knox-lock")]
        public async Task<IActionResult> KnoxLock(Guid id, string filter = StockPhoneRules.FilterAll)
        {
            if (!await CanManagePhone(id))
            {
                return RedirectToAction("Index", new { filter });
            }

            Flash(await _stock.KnoxApproveAndLockAsync(id, CurrentUser));
            return RedirectToAction("Index", new { filter });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/stock/{id:guid}/check")]
        public async Task<IActionResult> Check(Guid id, string filter = StockPhoneRules.FilterAll)
        {
            if (!await CanManagePhone(id))
            {
                return RedirectToAction("Index", new { filter });
            }

            Flash(await _stock.CheckLockAsync(id, CurrentUser));
            return RedirectToAction("Index", new { filter });
        }

        private void Flash(WorkflowResult result)
        {
            TempData[result.Success ? "StockMessages" : "StockErrors"] = new[] { result.Message };
        }

        private User CurrentUser => (User)HttpContext.Items["UserSettings"]!;

        private bool CanManage(out IActionResult? redirect)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            redirect = settings == null
                ? RedirectToAction("Index", "Login")
                : !StockPhoneRules.CanManage(settings) ? RedirectToAction("Index", "Enrolments") : null;
            return redirect == null;
        }

        // A dealer can only lock or check their own stock phones.
        private async Task<bool> CanManagePhone(Guid id)
        {
            if (HttpContext.Items["UserSettings"] is not User settings || !StockPhoneRules.CanManage(settings))
            {
                return false;
            }

            var e = await _enrolments.GetByEnrolmentIdAsync(id);
            return e != null && StockPhoneRules.CanSee(settings, e);
        }

        private void SetLayoutViewBags()
        {
            ViewBag.BackLink = "enrolments";
            ViewBag.IsAdmin = CurrentUser.RoleId == UserRole.Admin;
            ViewBag.IsApprover = CurrentUser.RoleId == UserRole.Approver;
            ViewBag.IsDealer = CurrentUser.RoleId == UserRole.Dealer;
            ViewBag.UserName = CurrentUser.KnownAs;
        }
    }
}
