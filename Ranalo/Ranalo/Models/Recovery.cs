namespace Ranalo.Models
{
    // Recovery (admin only): phones we're trying to get back under a lock.
    // See Services/RecoveryRules.cs and Database/Recovery/001.

    public static class RecoveryStatus
    {
        public const string Investigating = "Investigating";
        public const string UploadSent = "UploadSent";
        public const string Approved = "Approved";
        public const string LockSent = "LockSent";
        public const string MovedToKnox = "MovedToKnox";
        public const string NotCatchable = "NotCatchable";
        public const string Closed = "Closed";

        public static string Label(string status) => status switch
        {
            UploadSent => "Sent to Knox",
            Approved => "Approved in Knox",
            LockSent => "Lock sent",
            MovedToKnox => "Moved to Knox",
            NotCatchable => "Not catchable",
            _ => status,
        };
    }

    public static class RecoveryReason
    {
        public static readonly string[] All = { "Fraud", "Collections", "Other" };
    }

    public enum RecoveryAction
    {
        Upload,        // Veritech upload -> Knox
        UploadStatus,  // Veritech transaction status
        Approve,       // Knox approve with the Veritech transaction id
        KnoxCheck,     // Knox device list, searched by IMEI
        Lock,          // Knox lock now
        Unlock,        // Knox unlock
        MoveToKnox,    // Devices.LockGroup = 2 + Enrolments row: daily jobs take over
        NotCatchable,
        Close,
        Reopen,
        Note,
    }

    public class LockRecovery
    {
        public int Id { get; set; }
        public long AccountNo { get; set; }
        public string Imei { get; set; } = "";
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? CustomerName { get; set; }
        public string? DealerName { get; set; }
        public int? OriginalLockGroup { get; set; }
        public string Reason { get; set; } = "Other";
        public string Status { get; set; } = RecoveryStatus.Investigating;
        public string? VeritechTransId { get; set; }
        public string? Notes { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? MovedToKnoxAtUtc { get; set; }

        // Live from Devices.
        public int? CurrentLockGroup { get; set; }
        public bool? Locked { get; set; }
        public string? NextLockDate { get; set; }
        public string? LastConnectedAt { get; set; }
    }

    public class LockRecoveryAttempt
    {
        public int Id { get; set; }
        public int RecoveryId { get; set; }
        public string Step { get; set; } = "";
        public bool Success { get; set; }
        public string? Response { get; set; }
        public string? ByName { get; set; }
        public DateTime AtUtc { get; set; }
    }

    // What the add form fills in from the account.
    public class RecoveryDeviceLookup
    {
        public long AccountNo { get; set; }
        public string? Imei { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? CustomerName { get; set; }
        public string? DealerName { get; set; }
        public int? DealerId { get; set; }
        public int? LockGroup { get; set; }
    }

    public class RecoveryListViewModel
    {
        public string Status { get; set; } = "open";   // open | moved | ended | all
        public List<LockRecovery> Recoveries { get; set; } = new();
    }

    public class RecoveryDetailViewModel
    {
        public LockRecovery Recovery { get; set; } = null!;
        public List<LockRecoveryAttempt> Attempts { get; set; } = new();
        public IReadOnlySet<RecoveryAction> Actions { get; set; } = new HashSet<RecoveryAction>();
    }

    public record RecoveryActionResult(bool Success, string Message);
}
