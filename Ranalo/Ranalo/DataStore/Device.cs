namespace Ranalo.DataStore
{
    public class Device
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string ImeiNo { get; set; }
        public string ImeiNo2 { get; set; }
        public string SerialNo { get; set; }
        public bool IsTv { get; set; }
        public string? PhoneNumber { get; set; }
        public string Model { get; set; }
        public string Make { get; set; }
        public string OsVersion { get; set; }
        public string SdkVersion { get; set; }
        public string Status { get; set; }
        public bool Locked { get; set; }
        public string LockType { get; set; }
        public int? DeviceGroupId { get; set; }
        public string AdminLockType { get; set; }
        public bool AdminLocked { get; set; }
        public int? AppVersionCode { get; set; }
        public string? AppVersionName { get; set; }
        public string CreatedAt { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerPhoneNumber { get; set; }
        public string? UnlockCode { get; set; }
        public string? ValidityOfUnlockCode { get; set; }
        public bool IsActivated { get; set; }
        public bool IsLockedOnSimSwap { get; set; }
        public string? FirstLockDate { get; set; }
        public string? FirstLockDateIsoFormat { get; set; }
        public string NextLockDate { get; set; }
        public string? NextLockDateIsoFormat { get; set; }
        public string EulaStatus { get; set; }
        public string? EulaActionPerformedOn { get; set; }
        public string LastConnectedAt { get; set; }
        public bool? GettingStartedButtonClicked { get; set; }
        public string EnrollmentStatus { get; set; }
        public string? EnrollmentFailureReason { get; set; }
        public string AdditionalSetupDone { get; set; }
        public string BatteryOptimizationGranted { get; set; }
        public string EnrolledOn { get; set; }
        public string? DlcStatus { get; set; }
    }
}
