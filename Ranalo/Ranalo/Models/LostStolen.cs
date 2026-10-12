using Ranalo.DataStore.DataModels;

namespace Ranalo.Models
{
    // Lost & Stolen page. See Database/LostStolen/001 for the tables and
    // Services.LostStolenRules for who can do what.
    public static class LostStolenStatus
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string Rejected = "Rejected";
        public const string Closed = "Closed";
    }

    public static class LostStolenKind
    {
        public const string Lost = "Lost";
        public const string Stolen = "Stolen";
    }

    public static class LostStolenOutcome
    {
        public const string Found = "Found";
        public const string PaidOff = "PaidOff";
        public const string WrittenOff = "WrittenOff";

        public static string Label(string? outcome) => outcome switch
        {
            Found => "Found",
            PaidOff => "Paid off",
            WrittenOff => "Written off",
            _ => "-",
        };
    }

    public class LostStolenReport
    {
        public int Id { get; set; }
        public long AccountNo { get; set; }
        public string? CustomerName { get; set; }
        public string? ProductName { get; set; }
        public int? DealerId { get; set; }
        public string? DealerName { get; set; }
        public string Kind { get; set; } = LostStolenKind.Stolen;
        public DateTime IncidentDate { get; set; }
        public string? PoliceRef { get; set; }
        public string Description { get; set; } = "";
        public int ReportedByUserId { get; set; }
        public string? ReportedByName { get; set; }
        public string ReportedByRole { get; set; } = "";
        public DateTime ReportedAtUtc { get; set; }
        public string Status { get; set; } = LostStolenStatus.Pending;
        public string? DecidedByName { get; set; }
        public DateTime? DecidedAtUtc { get; set; }
        public string? DecisionNote { get; set; }
        public DateTime? LockSentAtUtc { get; set; }
        public string? LockResult { get; set; }
        public string? Outcome { get; set; }
        public string? ClosedByName { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public string? ClosedNote { get; set; }

        // Last time the phone checked in with its lock provider -- a stolen
        // phone that reappears shows here.
        public string? LastConnectedAt { get; set; }

        public List<LostStolenEvidence> Evidence { get; set; } = new();

        public bool IsOpen => Status is LostStolenStatus.Pending or LostStolenStatus.Confirmed;
    }

    public class LostStolenEvidence
    {
        public int Id { get; set; }
        public int ReportId { get; set; }
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public int SizeBytes { get; set; }
        public DateTime UploadedAtUtc { get; set; }
    }

    public class LostStolenEvidenceFile : LostStolenEvidence
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }

    // A file as uploaded, before it's checked and saved.
    public record LostStolenUpload(string FileName, string? ContentType, byte[] Content);

    public class NewLostStolenReport
    {
        public long AccountNo { get; set; }
        public string? Kind { get; set; }
        public DateTime? IncidentDate { get; set; }
        public string? PoliceRef { get; set; }
        public string? Description { get; set; }
    }

    // Who an account belongs to, for the scope rule (LostStolenRules.InScope).
    public class LostStolenAccountScope
    {
        public long AccountNo { get; set; }
        public bool HasDevice { get; set; }
        public int? DealerId { get; set; }
        public int? AssignedAgentId { get; set; }
        public int? CollectorUserId { get; set; }   // holds an open collections case on it
        public int? LockGroup { get; set; }
    }

    public class LostStolenViewModel
    {
        public string Tab { get; set; } = "open";
        public List<LostStolenReport> Reports { get; set; } = new();
        public List<AllAccounts> SearchResults { get; set; } = new();
        public string SearchTerm { get; set; } = "";
        public bool CanDecide { get; set; }
        public bool SetupMissing { get; set; }
        public int PendingCount { get; set; }
        public int ConfirmedCount { get; set; }
    }
}
