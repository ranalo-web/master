using Ranalo.DataStore.DataModels;

namespace Ranalo.Models
{
    // One tab of the Users page (Repository.GetUsersPageAsync): the logins on
    // this page, how many the tab has, and both tabs' counts.
    public record UsersPage(List<User> Users, int TotalCount, int ActiveCount, int SuspendedCount, Dictionary<UserRole, int> RoleCounts);

    public class UsersViewModel
    {
        public List<User>? Users { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public string? SearchTerm { get; set; }

        // Active (everyone who can log in) or Suspended.
        public bool Suspended { get; set; }
        public int ActiveCount { get; set; }
        public int SuspendedCount { get; set; }

        // The role tab picked (null = All) and active logins per role.
        public UserRole? Role { get; set; }
        public Dictionary<UserRole, int> RoleCounts { get; set; } = new();

        // Tabs in this order, shown only when the role has active logins.
        public static readonly (UserRole Role, string Label)[] RoleTabs =
        {
            (UserRole.Admin, "Admins"), (UserRole.Dealer, "Dealers"), (UserRole.Agent, "Agents"),
            (UserRole.Approver, "Approvers"), (UserRole.Collector, "Collectors"),
            (UserRole.Customer, "Customers"), (UserRole.Supplier, "Suppliers"), (UserRole.Guest, "Guests"),
        };

        // DealerId -> dealer name, for the Dealer column. 0 is Ranalo's own staff.
        public Dictionary<int, string> DealerNames { get; set; } = new();

        public string DealerName(int dealerId) =>
            dealerId == 0 ? "Ranalo (staff)"
            : DealerNames.TryGetValue(dealerId, out var name) ? name : $"Dealer {dealerId}";
    }
}
