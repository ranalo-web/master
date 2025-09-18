using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IUserService _userService;
        private readonly IApplicationReportService _reportService;
        public DashboardTotals Totals { get; set; } = new DashboardTotals();
        public List<CustomerDetails> Customers { get; set; } = new List<CustomerDetails>();

        public List<TransactionHistory> Transactions { get; set; } = new List<TransactionHistory>();

        public IndexModel(ILogger<IndexModel> logger, IUserService userService, IApplicationReportService reportService)
        {
            _logger = logger;
            _userService = userService;
            _reportService = reportService;
        }

        public async Task OnGet()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings != null)
            {
                ViewData["BackLink"] = "dashboard";
                ViewData["IsAdmin"] = settings.RoleId == UserRole.Admin;
                ViewData["IsApprover"] = settings.RoleId == UserRole.Approver;
                ViewData["UserName"] = settings.KnownAs; //settings.RoleId == 5;

                if (settings.RoleId == UserRole.Dealer)
                {
                    var dealer = await _userService.GetDealerByUserId(settings.UserId);
                    ViewData["UserName"] = dealer.CompanyName;
                    int dealerRef;
                    if (!int.TryParse(dealer.DealerReference, out dealerRef))
                    {
                        dealerRef = 0; // fallback if conversion fails
                    }
                    Totals = await _reportService.GetDashboardTotalsAsync(dealerRef);
                    Transactions = await _reportService.GetTransactionHistoryAsync(dealer.DealerId);
                    Customers = await _reportService.GetRecentCustomersAsync(dealer.DealerId);
                }
                else
                {
                    Totals = await _reportService.GetDashboardTotalsAsync();
                    Transactions = await _reportService.GetTransactionHistoryAsync();
                    Customers = await _reportService.GetRecentCustomersAsync();
                }

                    
                
                
            }
        }
    }
}
