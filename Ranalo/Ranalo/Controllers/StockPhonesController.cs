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
    // go to the field locked (Services/DeviceLock/StockPhoneRules.cs). Admin
    // only. Selling one is the normal Add Enrolment with the order.
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
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var all = await _enrolments.GetStockAsync(false, q?.Trim());
            filter = filter == StockPhoneRules.FilterUnverified ? filter : StockPhoneRules.FilterAll;
            var model = new StockPhonesViewModel
            {
                Filter = filter,
                Search = q?.Trim(),
                Total = all.Count,
                Unverified = all.Count(StockPhoneRules.NeedsVerifying),
                Rows = filter == StockPhoneRules.FilterUnverified ? all.Where(StockPhoneRules.NeedsVerifying).ToList() : all,
                Dealers = (await _users.GetAllDealers() ?? new List<DataStore.Dealer>())
                    .OrderBy(d => d.CompanyName).ToDictionary(d => d.DealerId, d => d.CompanyName),
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
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var (messages, errors) = await _stock.LockAsync(imeis, brand, dealerId, CurrentUser);
            TempData["StockMessages"] = messages.ToArray();
            TempData["StockErrors"] = errors.ToArray();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/stock/{id:guid}/knox-lock")]
        public async Task<IActionResult> KnoxLock(Guid id, string filter = StockPhoneRules.FilterAll)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            Flash(await _stock.KnoxApproveAndLockAsync(id, CurrentUser));
            return RedirectToAction("Index", new { filter });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("enrolments/stock/{id:guid}/check")]
        public async Task<IActionResult> Check(Guid id, string filter = StockPhoneRules.FilterAll)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            Flash(await _stock.CheckLockAsync(id, CurrentUser));
            return RedirectToAction("Index", new { filter });
        }

        private void Flash(WorkflowResult result)
        {
            TempData[result.Success ? "StockMessages" : "StockErrors"] = new[] { result.Message };
        }

        private User CurrentUser => (User)HttpContext.Items["UserSettings"]!;

        private bool IsAdmin(out IActionResult? redirect)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            redirect = settings == null
                ? RedirectToAction("Index", "Login")
                : !StockPhoneRules.CanManage(settings.RoleId) ? RedirectToAction("Index", "Enrolments") : null;
            return redirect == null;
        }

        private void SetLayoutViewBags()
        {
            ViewBag.BackLink = "enrolments";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = CurrentUser.KnownAs;
        }
    }
}
