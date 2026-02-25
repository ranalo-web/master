namespace Ranalo.SumsungKnox.Models
{
    public class SetBlinkingReminderRequest
    {
        public string? ObjectId { get; set; }
        public string? DeviceUid { get; set; }
        public string? ApproveId { get; set; }

        public string? Email { get; set; }
        public string? Tel { get; set; }

        public long Interval { get; set; }
        public string Message { get; set; } = null!;

        public bool? TimeLimitEnable { get; set; }
        public bool? DaysLimitEnable { get; set; }

        public int[]? TimeLimit { get; set; }
        public int[]? DaysLimit { get; set; }
    }
}
