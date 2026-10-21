using Ranalo.DataStore.DataModels;

namespace Ranalo.Services
{
    // Who can manage which logins (2026-10-21). Admins manage everyone;
    // dealers manage their own agents and collectors. Everyone manages their
    // own details and password on My Account -- not their own role or status.
    public static class UserAdminRules
    {
        private static readonly UserRole[] AdminAssignable =
            { UserRole.Admin, UserRole.Approver, UserRole.Dealer, UserRole.Agent, UserRole.Collector };

        private static readonly UserRole[] DealerAssignable = { UserRole.Agent, UserRole.Collector };

        public static IReadOnlyList<UserRole> AssignableRoles(User actor) => actor.RoleId switch
        {
            UserRole.Admin => AdminAssignable,
            UserRole.Dealer => DealerAssignable,
            _ => Array.Empty<UserRole>()
        };

        public static bool CanAddUsers(User actor) => AssignableRoles(actor).Count > 0;

        // actorDealerId: the dealer's Dealers.DealerId (a dealer login's own
        // Users.DealerId isn't reliably set; SetViewBags looks it up).
        public static bool CanManage(User actor, int actorDealerId, User target) =>
            actor.UserId != target.UserId && actor.RoleId switch
            {
                UserRole.Admin => true,
                UserRole.Dealer => target.DealerId == actorDealerId && DealerAssignable.Contains(target.RoleId),
                _ => false
            };

        public static bool CanAssign(User actor, UserRole role) => AssignableRoles(actor).Contains(role);

        public static string RoleName(UserRole role) => role switch
        {
            UserRole.Admin => "Admin",
            UserRole.Approver => "Approver",
            UserRole.Dealer => "Dealer",
            UserRole.Agent => "Agent",
            UserRole.Collector => "Collector",
            _ => role.ToString()
        };

        public static string Initials(string? first, string? last)
        {
            var a = string.IsNullOrWhiteSpace(first) ? "" : first.Trim()[..1];
            var b = string.IsNullOrWhiteSpace(last) ? "" : last.Trim()[..1];
            var initials = (a + b).ToUpperInvariant();
            return initials.Length > 0 ? initials : "?";
        }
    }
}
