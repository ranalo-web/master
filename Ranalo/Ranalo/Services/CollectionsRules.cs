namespace Ranalo.Services
{
    // Collections rules (agreed with the business 2026-10-01). Kept free of
    // I/O so they can be unit tested; see Database/Collections/001 for the
    // tables.
    //
    // Handover
    //   Admin hands an account to a collector from the pool: in arrears and
    //   nothing paid for 90 days. The collector can't be the account's agent
    //   or (a login of) its dealer.
    //   The agent's and dealer's deduction on that contract is frozen at the
    //   shortfall on the day -- the collector's recoveries never reduce it.
    //   Their unpaid bonus on it is cancelled (bonus already paid is kept),
    //   payments while it is in collections add nothing to the dealer's
    //   commission, and the account counts toward their default rate for good.
    //
    // Collector pay
    //   20% of every payment on the device while they hold the case,
    //   including a new customer's payments after a repossession and resale.
    //   Reassigning the case starts the new collector earning from then on.
    //   No suspension: poor performance is handled by reassigning, and an
    //   account with no payment for 90 days while held is flagged for it.
    //
    // Return to the agent/dealer
    //   Deduction = frozen amount + what collectors earned on the case
    //   + repossession cost + any new shortfall after the return.
    //   Sent to collections again: re-frozen at that deduction.
    public static class CollectionsRules
    {
        public const decimal CollectorShareRate = 0.20m;

        // Pool: no payment for this many days (and in arrears).
        public const int EligibleDaysWithoutPayment = 90;

        // A held account with no payment for this many days can be reassigned.
        public const int ReassignAfterDaysWithoutPayment = 90;

        public static decimal CollectorShare(decimal recovered) => Math.Round(Math.Max(0, recovered) * CollectorShareRate, 2);

        public static bool IsEligible(decimal shortfall, int daysSinceLastPayment) =>
            shortfall > 0 && daysSinceLastPayment >= EligibleDaysWithoutPayment;

        public static bool IsDueForReassignment(int daysWithoutPaymentWhileHeld) =>
            daysWithoutPaymentWhileHeld >= ReassignAfterDaysWithoutPayment;

        // The agent's and the dealer's arrears deduction on a contract that
        // has been in collections (each carries all of it, as with ordinary
        // arrears).
        public static decimal Deduction(CollectionTerms t, decimal liveShortfall) =>
            t.IsReturned
                ? t.FrozenDeduction + t.CollectorEarned + t.RepossessionCost + Math.Max(0, liveShortfall - t.ShortfallAtReturn)
                : t.FrozenDeduction;

        // Customer payments that count toward the dealer's commission: what
        // counted at handover, plus payments after a return.
        public static decimal DealerCountedPaid(CollectionTerms t, decimal liveTotalPaid) =>
            t.IsReturned
                ? t.CommissionPaidBasis + Math.Max(0, liveTotalPaid - t.TotalPaidAtReturn)
                : t.CommissionPaidBasis;

        // The agent keeps only bonus already paid on a collections account.
        public static decimal AgentBonusKept(decimal standardBonus, decimal bonusAlreadyPaid) =>
            Math.Max(0, Math.Min(standardBonus, bonusAlreadyPaid));

        // Who can be handed an account: not its agent, and not a dealer login
        // of its dealer.
        public static bool IsConflicted(CollectorCandidate collector, int? accountAgentUserId, int? accountDealerId) =>
            (accountAgentUserId.HasValue && collector.UserId == accountAgentUserId.Value)
            || (collector.IsDealerUser && accountDealerId.HasValue && collector.DealerId == accountDealerId.Value);
    }

    // The latest collections case on one contract, as the commission
    // calculator needs it.
    public class CollectionTerms
    {
        public int CaseId { get; set; }
        public bool IsReturned { get; set; }
        public decimal FrozenDeduction { get; set; }
        public decimal CommissionPaidBasis { get; set; }

        // Only used once returned.
        public decimal ShortfallAtReturn { get; set; }
        public decimal TotalPaidAtReturn { get; set; }
        public decimal CollectorEarned { get; set; }
        public decimal RepossessionCost { get; set; }
    }

    public class CollectorCandidate
    {
        public int UserId { get; set; }
        public int DealerId { get; set; }

        // A dealer login (Role Dealer) who is also a collector.
        public bool IsDealerUser { get; set; }
    }
}
