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
        private readonly ILogger<OperatingExpensesController> _logger;

        public OperatingExpensesController(IOperatingExpenseService expenseService, ILogger<OperatingExpensesController> logger)
        {
            _expenseService = expenseService;
            _logger = logger;
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

        [HttpGet]
        [Route("operating-expenses/template")]
        public IActionResult Template()
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            return File(OperatingExpenseUploadParser.BuildTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "OperatingExpensesTemplate.xlsx");
        }

        // Step 1 of a bulk upload: parse the file and show every row on a
        // review screen, where categories are assigned and mistakes fixed.
        // Nothing is saved here.
        [HttpPost]
        [Route("operating-expenses/upload")]
        public IActionResult Upload(IFormFile file)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }

            if (file == null || file.Length == 0)
            {
                TempData["ExpenseUploadError"] = "No file was selected. Choose a filled-in template and click Upload.";
                return RedirectToAction("Index");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xlsm")
            {
                TempData["ExpenseUploadError"] = $"'{file.FileName}' is not supported. Use the downloaded template (.xlsx).";
                return RedirectToAction("Index");
            }

            List<OperatingExpenseUploadRow> rows;
            try
            {
                rows = OperatingExpenseUploadParser.Parse(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operating expenses upload failed to parse {FileName}", file.FileName);
                TempData["ExpenseUploadError"] = $"Could not read '{file.FileName}': {ex.Message}";
                return RedirectToAction("Index");
            }

            if (rows.Count == 0)
            {
                TempData["ExpenseUploadError"] = $"'{file.FileName}' has no expense rows under the header.";
                return RedirectToAction("Index");
            }

            SetLayoutViewBags();
            return View("BulkReview", new OperatingExpenseBulkReviewViewModel { FileName = file.FileName, Rows = rows });
        }

        // Step 2: save the reviewed rows. Every included row is re-validated
        // (category now required); if any fail, the review screen is shown
        // again with the errors and nothing is saved.
        [HttpPost]
        [Route("operating-expenses/bulk-save")]
        public async Task<IActionResult> BulkSave(OperatingExpenseBulkReviewViewModel model)
        {
            if (!IsAdmin(out var redirect))
            {
                return redirect!;
            }
            var settings = (User)HttpContext.Items["UserSettings"]!;

            var included = model.Rows.Where(r => r.Include).ToList();
            foreach (var row in model.Rows)
            {
                row.Errors.Clear();
                row.Description = row.Description?.Trim();
                if (row.Include)
                {
                    OperatingExpenseUploadParser.Validate(row, requireCategory: true);
                }
            }

            if (included.Count == 0 || included.Any(r => r.Errors.Any()))
            {
                ViewBag.SaveError = included.Count == 0
                    ? "No rows are ticked to save."
                    : $"{included.Count(r => r.Errors.Any())} row(s) need fixing before anything can be saved.";
                SetLayoutViewBags();
                return View("BulkReview", model);
            }

            foreach (var row in included)
            {
                await _expenseService.AddAsync(row.ExpenseDate!.Value, row.Category!, row.Description, row.Amount!.Value, settings.UserId);
            }

            var first = included.Min(r => r.ExpenseDate!.Value);
            var last = included.Max(r => r.ExpenseDate!.Value);
            _logger.LogInformation("Bulk-saved {Count} operating expenses from {FileName} by user {UserId}", included.Count, model.FileName, settings.UserId);

            TempData["ExpenseUploadSuccess"] = $"Saved {included.Count:N0} expenses totalling KES {included.Sum(r => r.Amount!.Value):N0} " +
                $"({first:dd MMM yyyy} – {last:dd MMM yyyy})" +
                (model.Rows.Count > included.Count ? $", {model.Rows.Count - included.Count:N0} row(s) left out." : ".");

            // Show the month of the latest expense saved.
            return RedirectToAction("Index", new { year = last.Year, month = last.Month });
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
            ViewBag.BackLink = "operating-expenses";
            ViewBag.IsAdmin = true;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.UserName = settings.KnownAs;
            ViewBag.Categories = OperatingExpenseCategory.All;
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
