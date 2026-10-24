using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services
{
    // Who may change an account's commission status, and what a change
    // needs. Admin only, and every change needs a note saying why (it is
    // kept in CommissionExclusionLog). No I/O.
    public static class CommissionExclusionRules
    {
        public const int MaxNotesLength = 500;

        public static bool CanChange(UserRole roleId) => roleId == UserRole.Admin;

        public static string? Normalize(string? status) =>
            new[] { CommissionStatus.Qualifies, CommissionStatus.NoCommission, CommissionStatus.Recover }
                .FirstOrDefault(s => string.Equals(s, status?.Trim(), StringComparison.OrdinalIgnoreCase));

        // Null when the change is allowed, else what's wrong.
        public static string? Check(UserRole roleId, long contractId, string? status, string? reason, string? notes)
        {
            if (!CanChange(roleId))
            {
                return "Only an admin can change an account's commission.";
            }
            if (contractId <= 0)
            {
                return "That account has no contract.";
            }

            var s = Normalize(status);
            if (s == null)
            {
                return "Choose what the account should earn.";
            }
            if (s != CommissionStatus.Qualifies && CommissionExclusionReason.Normalize(reason) == null)
            {
                return "Choose a reason: Legacy, Fraud or Other.";
            }
            if (string.IsNullOrWhiteSpace(notes))
            {
                return "Add a note saying why -- it is kept with the change.";
            }
            if (notes.Trim().Length > MaxNotesLength)
            {
                return $"Keep the note under {MaxNotesLength} characters.";
            }
            return null;
        }

        // Account Commissions page filter: all, or one status.
        public static bool Matches(CommissionBreakdown c, string? filter) => Normalize(filter) switch
        {
            CommissionStatus.Qualifies => !c.NoCommission,
            CommissionStatus.NoCommission => c.NoCommission && !c.RecoverArrears,
            CommissionStatus.Recover => c.RecoverArrears,
            _ => true,
        };
    }
}
