using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ranalo.ViewComponents
{
    // "Waiting for you" on the Admin Dashboard: everything an admin has to
    // decide, with a count and a link to work through it one by one. Each
    // count is read on its own, so one that fails (a table not created yet)
    // just isn't shown -- the dashboard never breaks. Nothing waiting, no panel.
    public class WaitingForYouViewComponent : ViewComponent
    {
        public record Item(string Label, string Hint, int Count, string Link, string Icon);

        private static readonly (string Label, string Hint, string Link, string Icon, string Sql)[] Checks =
        {
            ("Write-offs to review", "Proposed by the nightly check; approve or hold each one.", "/write-offs", "ni-file-minus",
                "SELECT COUNT(*) FROM WriteOffs WHERE Status = 'Pending'"),
            ("Self-service plans", "Customers put themselves on a plan; check within 24 hours.", "/self-restructures", "ni-repeat",
                "SELECT COUNT(*) FROM SelfRestructures WHERE Status = 'Active' AND ReviewedAtUtc IS NULL"),
            ("Phones to release", "Fully paid: approve removing each from its lock provider.", "/pending-tasks", "ni-check-round-cut",
                "SELECT COUNT(*) FROM DeviceRemovalTasks WHERE Status IN ('Pending', 'Failed')"),
            ("Give to a collector", "Approvers couldn't sort these out: in the pool to assign.", "/collections-admin?tab=pool", "ni-user-check",
                "SELECT COUNT(*) FROM ApproverFollowUps WHERE Status = 'ToCollections'"),
            ("Collector flags", "Raised by collectors on their cases.", "/collections-admin?tab=flags", "ni-flag",
                "SELECT COUNT(*) FROM CollectionFlags WHERE ResolvedAt IS NULL"),
            ("Customer app locked", "Customers who emailed in to be unlocked.", "/customer-app?show=locked", "ni-mobile",
                "SELECT COUNT(*) FROM CustomerAccess WHERE LockedAtUtc IS NOT NULL"),
            ("Recoveries in progress", "Waiting for the next step (upload, approve, lock).", "/recovery", "ni-shield-alert",
                "SELECT COUNT(*) FROM LockRecoveries WHERE Status NOT IN ('Closed', 'NotCatchable', 'MovedToTranssion', 'MovedToKnox')"),
        };

        // The approvers' own list (Approver Dashboard).
        private static readonly (string Label, string Hint, string Link, string Icon, string Sql)[] ApproverChecks =
        {
            ("Follow-ups to pick up", "Behind, no payment for 90 days: nobody has taken them yet.", "/approver-follow-ups", "ni-call-alt",
                "SELECT COUNT(*) FROM ApproverFollowUps WHERE Status = 'Open' AND TakenByUserId IS NULL"),
            ("Promises due", "Customers who promised to pay by today: check they did.", "/approver-follow-ups", "ni-calendar-check",
                "SELECT COUNT(*) FROM ApproverFollowUps WHERE Status = 'Open' AND PromisedDate <= CAST(DATEADD(HOUR, 3, SYSUTCDATETIME()) AS DATE)"),
            ("Customer app locked", "Customers who emailed in to be unlocked.", "/customer-app?show=locked", "ni-mobile",
                "SELECT COUNT(*) FROM CustomerAccess WHERE LockedAtUtc IS NOT NULL"),
        };

        private readonly IDbConnection _db;
        private readonly ILogger<WaitingForYouViewComponent> _logger;

        public WaitingForYouViewComponent(IDbConnection db, ILogger<WaitingForYouViewComponent> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync(bool forApprover = false)
        {
            var items = new List<Item>();
            foreach (var c in forApprover ? ApproverChecks : Checks)
            {
                try
                {
                    var count = await _db.ExecuteScalarAsync<int>(c.Sql);
                    if (count > 0) items.Add(new Item(c.Label, c.Hint, count, c.Link, c.Icon));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Waiting for you: couldn't count {Label}", c.Label);
                }
            }
            return View(items);
        }
    }
}
