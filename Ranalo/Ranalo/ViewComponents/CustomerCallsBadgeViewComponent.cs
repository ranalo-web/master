using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Ranalo.Services;

namespace Ranalo.ViewComponents
{
    // Calls waiting for approvers and admins, next to Customer Calls in the
    // menu (CustomerScreeningService.GetBadgeCountAsync). Renders nothing when
    // there are none or the count can't be read -- the menu must never break the page.
    public class CustomerCallsBadgeViewComponent : ViewComponent
    {
        private readonly ICustomerScreeningService _screening;
        private readonly ILogger<CustomerCallsBadgeViewComponent> _logger;

        public CustomerCallsBadgeViewComponent(ICustomerScreeningService screening, ILogger<CustomerCallsBadgeViewComponent> logger)
        {
            _screening = screening;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var count = await _screening.GetBadgeCountAsync();
                if (count > 0)
                {
                    return new HtmlContentViewComponentResult(
                        new HtmlString($"<span class=\"badge rounded-pill bg-danger ms-1\">{count}</span>"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Couldn't count customer calls");
            }

            return new HtmlContentViewComponentResult(HtmlString.Empty);
        }
    }
}
