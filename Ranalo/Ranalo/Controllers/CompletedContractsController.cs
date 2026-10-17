using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Completed contracts: closed contracts and their details
    // (Services/CompletedContractRules.cs). Admin only, read only.
    [LoadUserSettingsFromCookie]
    public class CompletedContractsController : Controller
    {
        private readonly ICompletedContractRepository _repository;

        public CompletedContractsController(ICompletedContractRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        [Route("completed-contracts")]
        public async Task<IActionResult> Index(string filter = CompletedFilter.PaidOff, string? q = null, int page = 1)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            SetLayoutViewBags();
            return View(CompletedContractRules.Build(await _repository.GetClosedAsync(), filter, q, page));
        }

        [HttpGet]
        [Route("completed-contracts/{id:int}")]
        public async Task<IActionResult> Details(int id, int page = 1)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            var contract = await _repository.GetAsync(id);
            if (contract == null)
            {
                return RedirectToAction("Index");
            }

            CompletedContractRules.Evaluate(contract);
            var (payments, total) = await _repository.GetPaymentsAsync(id, page, CompletedContractRules.PageSize);
            SetLayoutViewBags();
            return View(new CompletedContractDetailViewModel
            {
                Contract = contract,
                Payments = payments,
                PaymentsPage = Math.Max(page, 1),
                PaymentsPages = Math.Max(1, (int)Math.Ceiling(total / (double)CompletedContractRules.PageSize)),
            });
        }

        private bool IsAdmin(out IActionResult? redirect)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            redirect = settings == null
                ? RedirectToAction("Index", "Login")
                : settings.RoleId != UserRole.Admin ? RedirectToAction("Index", "Home") : null;
            return redirect == null;
        }

        private void SetLayoutViewBags()
        {
            var settings = (User)HttpContext.Items["UserSettings"]!;
            ViewBag.BackLink = "completed-contracts";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
        }
    }
}
