using Ranalo.DataStore.DataModels;

namespace Ranalo.Services
{
    // Who may create a manual restructure, and to what rate (2026-10-22):
    //   - Admin: any rate;
    //   - Approver: no lower than the account's daily payment (daily +
    //     weekly / 7 + monthly / 30, RestructureCalculator.DailyPaymentAll);
    //   - everyone else: not at all.
    public static class RestructurePermissions
    {
        public static bool CanRestructure(UserRole role) =>
            role is UserRole.Admin or UserRole.Approver;

        // The lowest rate this role may set, null = no lower limit.
        public static decimal? MinimumRate(UserRole role, decimal dailyPaymentAll) =>
            role == UserRole.Approver ? Math.Round(dailyPaymentAll, 2) : null;

        // null = allowed, otherwise why not.
        public static string? Refusal(UserRole role, decimal newRate, decimal? dailyPaymentAll)
        {
            if (!CanRestructure(role))
            {
                return "You don't have permission to restructure accounts.";
            }
            if (newRate <= 0)
            {
                return "The new rate must be more than 0.";
            }
            if (role == UserRole.Approver)
            {
                if (dailyPaymentAll is not > 0)
                {
                    return "Couldn't find this account's daily payment, so an approver can't restructure it. Ask an admin.";
                }

                var minimum = MinimumRate(role, dailyPaymentAll.Value)!.Value;
                if (newRate < minimum)
                {
                    return $"Approvers can't set a rate below the account's daily payment of KES {minimum:N2}. Ask an admin for a lower rate.";
                }
            }

            return null;
        }
    }
}
