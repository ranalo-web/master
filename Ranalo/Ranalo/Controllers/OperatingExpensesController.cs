using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    // Operating Expenses ledger -- feeds the Financials page's Income
    // Statement ("Less: Operating expenses") and monthly comparison chart.
    // Admin-only: this is financial data, same sensitivity as the rest of
    // the Financials page.
    [LoadUserSettingsFromCookie]
    public class OperatingExpensesController : Controller
    {
        private readonly IOperatingExpenseService _expenseService;

        public OperatingExpensesController(IOperatingExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        [HttpGet]
        [Route("operating-expenses")]
        public async Task<IActionResult> Index(int? year = null, int? month = null, int page = 1, int pageSize = 20)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "operating-expenses";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
            ViewBag.Categories = OperatingExpenseCategory.All;

            var now = DateTime.Now;
            var resolvedYear = year ?? now.Year;
            var resolvedMonth = month ?? now.Month;

            var (expenses, totalRecords, monthTotal) = await _expenseService.GetForMonthAsync(resolvedYear, resolvedMonth, page, pageSize);

            var model = new OperatingExpensesViewModel
            {
                Expenses = expenses,
                MonthTotal = monthTotal,
                Year = resolvedYear,
                Month = resolvedMonth,
                CurrentPage = page,
                TotalRecords = totalRecords,
                TotalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize)),
            };

            return View(model);
        }

        [HttpPost]
        [Route("operating-expenses/add")]
        public async Task<IActionResult> Add(DateTime expenseDate, string category, string? description, decimal amount, int year, int month)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            if (amount > 0 && OperatingExpenseCategory.All.Contains(category))
            {
                await _expenseService.AddAsync(expenseDate, category, description, amount, settings.UserId);
            }

            return RedirectToAction("Index", new { year, month });
        }

        [HttpPost]
        [Route("operating-expenses/{id:int}/delete")]
        public async Task<IActionResult> Delete(int id, int year, int month)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            await _expenseService.RemoveAsync(id, settings.UserId);

            return RedirectToAction("Index", new { year, month });
        }
    }
}
