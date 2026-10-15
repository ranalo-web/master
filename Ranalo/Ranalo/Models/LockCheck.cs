namespace Ranalo.Models
{
    // Lock check (admin only): Nuovo phones that need attention. See
    // Services/LockCheckRules.cs and Database/LockCheck/001.

    public enum LockCheckStage
    {
        None,             // nothing to do
        NeedsNewLocker,   // unlocked, a collections case (in arrears, no payment for 90+ days); Samsung or itel/TECNO/Infinix: send to Recovery
        NuovoOnly,        // same, other brands: send a Nuovo lock
        LockSent,         // a Nuovo lock was sent from here; still unlocked
        InRecovery,       // already in Recovery
        LostFromNuovo,    // Nuovo no longer has it (un_registered) and money is owed
        FullyPaidOpen,    // paid off, contract still open: close and release
        Restructured,     // unlocked but on an agreed restructure: not a bad customer, no action
    }

    public enum LockCheckBrand
    {
        Samsung,
        Transsion,
        Other,
    }

    public static class LockCheckAction
    {
        public const string ToRecovery = "ToRecovery";
        public const string NuovoLock = "NuovoLock";
        public const string CloseRelease = "CloseRelease";
    }

    // One Nuovo phone with an open contract, as read from the database.
    public class LockCheckRow
    {
        public long AccountNo { get; set; }
        public int ContractId { get; set; }
        public string? CustomerName { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? Imei { get; set; }
        public string? NuovoStatus { get; set; }
        public bool? Locked { get; set; }
        public string? NextLockDate { get; set; }
        public string? LastConnectedAt { get; set; }
        public int? LockGroup { get; set; }
        public string? DealerName { get; set; }
        public decimal Deposit { get; set; }
        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
        public decimal TermInMonths { get; set; }
        public DateTime? StartDate { get; set; }
        public decimal Paid { get; set; }
        public DateTime? LastPaid { get; set; }
        public int? RecoveryId { get; set; }
        public string? RecoveryStatus { get; set; }
        public string? LastAction { get; set; }
        public bool? LastActionSuccess { get; set; }
        public DateTime? LastActionAt { get; set; }
        public string? LastActionResult { get; set; }
        public bool Restructured { get; set; }   // has an agreed restructure (RestructuredRecords)

        // Worked out by LockCheckRules.
        public LockCheckBrand Brand { get; set; }
        public decimal ContractValue { get; set; }
        public decimal Arrears { get; set; }       // what's owed to date, 0 if up to date
        public int DaysBehind { get; set; }
        public int DaysSinceLastPayment { get; set; }   // since the last payment, or the start if never paid
        public bool FullyPaid { get; set; }
        public LockCheckStage Stage { get; set; }
    }

    public class LockCheckViewModel
    {
        public LockCheckStage Stage { get; set; } = LockCheckStage.NeedsNewLocker;
        public string Brand { get; set; } = "all";   // all | samsung | transsion | other
        public int MinDaysWithoutPayment { get; set; } = 90;
        public Dictionary<LockCheckStage, int> Counts { get; set; } = new();
        public List<LockCheckRow> Rows { get; set; } = new();
    }

    public record LockCheckResult(int Done, int Failed, List<string> Messages);
}
