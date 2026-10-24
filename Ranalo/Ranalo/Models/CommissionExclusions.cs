namespace Ranalo.Models
{
    // A contract that earns no commission for anyone (Database/Commissions/005).
    // Legacy = sold before the current commission rules (before 1 Oct 2026,
    // except Mali Mali); those were paid under the old calculators. With
    // RecoverArrears on, the customer's shortfall is still deducted from the
    // dealer's commission on their other accounts -- how legacy arrears are
    // recovered from a dealer, one account at a time.
    public class CommissionExclusion
    {
        public long ContractId { get; set; }
        public string Reason { get; set; } = CommissionExclusionReason.Legacy;
        public bool RecoverArrears { get; set; }
        public string? Notes { get; set; }
        public int? SetByUserId { get; set; }
        public string? SetByName { get; set; }
        public DateTime SetAtUtc { get; set; }
    }

    public static class CommissionExclusionReason
    {
        public const string Legacy = "Legacy";
        public const string Fraud = "Fraud";
        public const string Other = "Other";

        public static readonly string[] All = { Legacy, Fraud, Other };

        public static string? Normalize(string? reason) =>
            All.FirstOrDefault(r => string.Equals(r, reason?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // What an admin can set a contract to on the Account Commissions page.
    public static class CommissionStatus
    {
        public const string Qualifies = "Qualifies";
        public const string NoCommission = "NoCommission";
        public const string Recover = "Recover";

        public static string Of(CommissionExclusion? x) =>
            x == null ? Qualifies : x.RecoverArrears ? Recover : NoCommission;

        public static string Label(string status) => status switch
        {
            NoCommission => "No commission",
            Recover => "No commission, recovering arrears",
            _ => "Earns commission",
        };
    }

    // One change to a contract's commission status, kept for good.
    public class CommissionExclusionLogEntry
    {
        public long ContractId { get; set; }
        public string OldStatus { get; set; } = "";
        public string NewStatus { get; set; } = "";
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public int? ChangedByUserId { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }
}
