
using Newtonsoft.Json;

namespace Ranalo.Woocommece.Api.Models
{

    public class LockDevices
    {
        //total_count
        [JsonProperty("total_count")]
        public int TotalCount { get; set; }
        [JsonProperty("current_page")]
        public int CurrentPage { get; set; }
        [JsonProperty("prev_page")]
        public int? PreviousPage { get; set; }
        [JsonProperty("next_page")]
        public int? NextPage { get; set; }
        [JsonProperty("devices")]
        public List<Device>? Devices { get; set; }
    }
    public class Device
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("imei_no")]
        public string ImeiNo { get; set; }

        [JsonProperty("imei_no2")]
        public string ImeiNo2 { get; set; }

        [JsonProperty("serial_no")]
        public string SerialNo { get; set; }

        [JsonProperty("is_tv")]
        public bool IsTv { get; set; }

        [JsonProperty("phone_number")]
        public string? PhoneNumber { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("make")]
        public string Make { get; set; }

        [JsonProperty("os_version")]
        public string OsVersion { get; set; }

        [JsonProperty("sdk_version")]
        public string SdkVersion { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("locked")]
        public bool Locked { get; set; }

        [JsonProperty("lock_type")]
        public string LockType { get; set; }

        [JsonProperty("device_group_id")]
        public int? DeviceGroupId { get; set; }

        [JsonProperty("admin_lock_type")]
        public string AdminLockType { get; set; }

        [JsonProperty("admin_locked")]
        public bool AdminLocked { get; set; }

        [JsonProperty("app_version_code")]
        public int? AppVersionCode { get; set; }

        [JsonProperty("app_version_name")]
        public string? AppVersionName { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("customer_name")]
        public string? CustomerName { get; set; }

        [JsonProperty("customer_email")]
        public string? CustomerEmail { get; set; }

        [JsonProperty("customer_address")]
        public string? CustomerAddress { get; set; }

        [JsonProperty("customer_phone_number")]
        public string? CustomerPhoneNumber { get; set; }

        [JsonProperty("unlock_code")]
        public string? UnlockCode { get; set; }

        [JsonProperty("validity_of_unlock_code")]
        public string? ValidityOfUnlockCode { get; set; }

        [JsonProperty("is_activated")]
        public bool IsActivated { get; set; }

        [JsonProperty("is_locked_on_sim_swap")]
        public bool IsLockedOnSimSwap { get; set; }

        [JsonProperty("first_lock_date")]
        public string? FirstLockDate { get; set; }

        [JsonProperty("first_lock_date_in_iso_format")]
        public string? FirstLockDateIsoFormat { get; set; }

        [JsonProperty("next_lock_date")]
        public string NextLockDate { get; set; }

        [JsonProperty("next_lock_date_in_iso_format")]
        public string? NextLockDateIsoFormat { get; set; }

        [JsonProperty("custom_fields")]
        public List<object> CustomFields { get; set; } = new();

        [JsonProperty("eula_status")]
        public string EulaStatus { get; set; }

        [JsonProperty("eula_action_performed_on")]
        public string? EulaActionPerformedOn { get; set; }

        [JsonProperty("sim_lock_info")]
        public SimLockInfo SimLockInfo { get; set; }

        [JsonProperty("last_connected_at")]
        public string LastConnectedAt { get; set; }

        [JsonProperty("getting_started_button_clicked")]
        public bool? GettingStartedButtonClicked { get; set; }

        [JsonProperty("enrollment_status")]
        public string EnrollmentStatus { get; set; }

        [JsonProperty("device_setting_attributes")]
        public DeviceSettingAttributes DeviceSettingAttributes { get; set; }

        [JsonProperty("enrollment_failure_reason")]
        public string? EnrollmentFailureReason { get; set; }

        [JsonProperty("additional_setup_done")]
        public string AdditionalSetupDone { get; set; }

        [JsonProperty("battery_optimization_granted")]
        public string BatteryOptimizationGranted { get; set; }

        [JsonProperty("enrolled_on")]
        public string EnrolledOn { get; set; }

        [JsonProperty("dlc_status")]
        public string? DlcStatus { get; set; }
    }

    public class SimLockInfo
    {
        [JsonProperty("approved")]
        public List<SimInfo> Approved { get; set; }

        [JsonProperty("unapproved")]
        public List<SimInfo> Unapproved { get; set; }

        [JsonProperty("auto_approve_new_sim")]
        public AutoApproveNewSim AutoApproveNewSim { get; set; }
    }

    public class SimInfo
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("imsi")]
        public string Imsi { get; set; }

        [JsonProperty("iccid")]
        public string Iccid { get; set; }

        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; }
    }

    public class AutoApproveNewSim
    {
        [JsonProperty("state")]
        public string? State { get; set; }

        [JsonProperty("expiry_time")]
        public string? ExpiryTime { get; set; }
    }

    public class DeviceSettingAttributes
    {
        [JsonProperty("lock_on_sim_swap")]
        public string LockOnSimSwap { get; set; }

        [JsonProperty("disable_unknown_sources")]
        public string? DisableUnknownSources { get; set; }

        //[JsonProperty("mandatory_packages")]
        //public string? MandatoryPackages { get; set; }

        [JsonProperty("auto_lock_schedule")]
        public AutoLockSchedule AutoLockSchedule { get; set; }
    }

    public class AutoLockSchedule
    {
        [JsonProperty("scheduler_id")]
        public int? SchedulerId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public class DlcDetails
    {
        [JsonProperty("dlc_status")]
        public string DlcStatus { get; set; }
    }
}
