using Ranalo.Models;

namespace Ranalo.Services
{
    // Approver follow-ups (agreed 2026-11-06): an account that reaches the
    // collections pool (CollectionsRules.IsEligible: behind, no payment for
    // 90 days, no open case) goes to the Approvers first, to sort out in
    // house. While they have it, it's out of the admin pool. If they can't,
    // they send it to collections and an admin gives it to a collector.
    //
    //   Open           with the Approvers (added by the job)
    //   ToCollections  Approvers gave up; back in the admin pool, flagged
    //   Closed         paying again / in collections / contract ended --
    //                  always closed by the job from the facts, never by hand,
    //                  so "paying" means a payment actually arrived.
    public static class ApproverFollowUpRules
    {
        public const string Open = "Open";
        public const string ToCollections = "ToCollections";
        public const string Closed = "Closed";

        public const string Paying = "Paying again";
        public const string InCollections = "In collections";
        public const string ContractEnded = "Contract ended";

        // Every few hours: new pool accounts reach the Approvers the same day.
        public static readonly TimeSpan JobInterval = TimeSpan.FromHours(4);

        public static bool ShouldAdd(CollectionContractStanding s, bool hasOpenCase, bool hasActiveFollowUp) =>
            !hasActiveFollowUp && !hasOpenCase && CollectionsRules.IsEligible(s.Shortfall, s.DaysSinceLastPayment);

        // Why an active follow-up is finished, or null while it still stands.
        // standing is null when the contract is no longer open.
        public static string? CloseReason(CollectionContractStanding? standing, bool hasOpenCase)
        {
            if (hasOpenCase) return InCollections;
            if (standing == null) return ContractEnded;
            if (!CollectionsRules.IsEligible(standing.Shortfall, standing.DaysSinceLastPayment)) return Paying;
            return null;
        }

        // In the admin pool: everything eligible except what the Approvers
        // are still working on.
        public static bool InAdminPool(bool eligibleNoCase, string? followUpStatus) =>
            eligibleNoCase && followUpStatus != Open;
    }
}
