using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;

namespace Ranalo.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IUserService _userService;

        public IndexModel(ILogger<IndexModel> logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
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
                }
            }
        }
    }
}
