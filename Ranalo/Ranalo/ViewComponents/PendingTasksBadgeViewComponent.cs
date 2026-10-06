using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Ranalo.DataStore;

namespace Ranalo.ViewComponents
{
    // Count of removals waiting for an admin, shown next to Pending Tasks in
    // the admin menu. Renders nothing when there are none or the count can't
    // be read -- the menu must never break the page.
    public class PendingTasksBadgeViewComponent : ViewComponent
    {
        private readonly IDeviceRemovalTaskRepository _tasks;
        private readonly ILogger<PendingTasksBadgeViewComponent> _logger;

        public PendingTasksBadgeViewComponent(IDeviceRemovalTaskRepository tasks, ILogger<PendingTasksBadgeViewComponent> logger)
        {
            _tasks = tasks;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var count = await _tasks.CountAwaitingDecisionAsync();
                if (count > 0)
                {
                    return new HtmlContentViewComponentResult(
                        new HtmlString($"<span class=\"badge rounded-pill bg-danger ms-1\">{count}</span>"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Couldn't count pending removal tasks");
            }

            return new HtmlContentViewComponentResult(HtmlString.Empty);
        }
    }
}
