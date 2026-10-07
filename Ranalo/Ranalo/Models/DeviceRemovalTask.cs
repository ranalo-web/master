namespace Ranalo.Models
{
    // A row of dbo.DeviceRemovalTasks: a fully-paid device waiting for an
    // admin to approve removing it from its lock provider.
    public class DeviceRemovalTask
    {
        public int Id { get; set; }
        public long AccountId { get; set; }
        public string? Imei { get; set; }
        public string? CustomerName { get; set; }
        public int? LockGroup { get; set; }
        public string Provider { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Status { get; set; } = DeviceRemovalStatus.Pending;
        public DateTime CreatedAtUtc { get; set; }
        public int? DecidedByUserId { get; set; }
        public DateTime? DecidedAtUtc { get; set; }
        public string? DecisionNote { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastAttemptAtUtc { get; set; }
        public string? LastResponse { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    public static class DeviceRemovalStatus
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Rejected = "Rejected";

        // List filter: what an admin still has to act on.
        public const string Awaiting = "Awaiting";
        public const string All = "All";
    }

    public class PendingTasksViewModel
    {
        public List<DeviceRemovalTask> Tasks { get; set; } = new();
        public string Status { get; set; } = DeviceRemovalStatus.Awaiting;
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
        public List<string> Messages { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
