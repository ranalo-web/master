using System.Text.Json.Serialization;

namespace Ranalo.SumsungKnox.Models
{
    public class ListDevicesResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
        [JsonPropertyName("deviceList")]
        public List<DeviceDto>? DeviceList { get; set; }
        [JsonPropertyName("error")]
        public List<object>? Error { get; set; }
    }

    public class DeviceDto
    {
        [JsonPropertyName("objectId")]
        public string? ObjectId { get; set; }
        [JsonPropertyName("deviceUid")]
        public string? DeviceUid { get; set; }
        [JsonPropertyName("approveId")]
        public string? ApproveId { get; set; }
        [JsonPropertyName("approveComment")]
        public string? ApproveComment { get; set; }
        [JsonPropertyName("createDate")]
        public long CreateDate { get; set; }
        [JsonPropertyName("modifiedDate")]
        public long ModifiedDate { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        [JsonPropertyName("isDeviceSupportHotp")]
        public bool IsDeviceSupportHotp { get; set; }
        [JsonPropertyName("isOfflineLocked")]
        public bool IsOfflineLocked { get; set; }
        [JsonPropertyName("isDeviceOffline")]
        public bool IsDeviceOffline { get; set; }
        [JsonPropertyName("simControlEnabled")]
        public bool SimControlEnabled { get; set; }
        [JsonPropertyName("simControlApplied")]
        public bool SimControlApplied { get; set; }
        [JsonPropertyName("lastSeen")]
        public long LastSeen { get; set; }
        [JsonPropertyName("relockTimestamp")]
        public long RelockTimestamp { get; set; }
        [JsonPropertyName("latestRelockApplied")]
        public bool LatestRelockApplied { get; set; }
        [JsonPropertyName("autoLockTarget")]
        public bool AutoLockTarget { get; set; }
        [JsonPropertyName("isRelockReminderTarget")]
        public bool IsRelockReminderTarget { get; set; }
        [JsonPropertyName("isRelockReminderApplied")]
        public bool IsRelockReminderApplied { get; set; }
        [JsonPropertyName("isFactoryResetBlockTarget")]
        public bool IsFactoryResetBlockTarget { get; set; }
        [JsonPropertyName("isFactoryResetBlockApplied")]
        public bool IsFactoryResetBlockApplied { get; set; }
        [JsonPropertyName("isOfflineLockTarget")]
        public bool IsOfflineLockTarget { get; set; }
        [JsonPropertyName("isOfflineLockApplied")]
        public bool IsOfflineLockApplied { get; set; }
        [JsonPropertyName("isEnrollmentNoticeTarget")]
        public bool IsEnrollmentNoticeTarget { get; set; }
        [JsonPropertyName("isFunctionRestrictionsTarget")]
        public bool IsFunctionRestrictionsTarget { get; set; }
        [JsonPropertyName("isFunctionRestrictionsApplied")]
        public bool IsFunctionRestrictionsApplied { get; set; }
        [JsonPropertyName("isForceAutoDownloadTarget")]
        public bool IsForceAutoDownloadTarget { get; set; }
        [JsonPropertyName("isForceAutoDownloadApplied")]
        public bool IsForceAutoDownloadApplied { get; set; }
        [JsonPropertyName("hsModeApplied")]
        public bool HsModeApplied { get; set; }
        [JsonPropertyName("isWallpaperRestrictionsTarget")]
        public bool IsWallpaperRestrictionsTarget { get; set; }
        [JsonPropertyName("isWallpaperRestrictionsApplied")]
        public bool IsWallpaperRestrictionsApplied { get; set; }
        [JsonPropertyName("isKge")]
        public bool IsKge { get; set; }
        [JsonPropertyName("offlineLock")]

        public object? OfflineLock { get; set; }
        [JsonPropertyName("enrollmentNotice")]
        public object? EnrollmentNotice { get; set; }
        [JsonPropertyName("customerApp")]
        public object? CustomerApp { get; set; }
        [JsonPropertyName("customerAppList")]
        public List<object>? CustomerAppList { get; set; }
        [JsonPropertyName("languageSettings")]
        public object? LanguageSettings { get; set; }
        [JsonPropertyName("agentVersion")]

        public string? AgentVersion { get; set; }
        [JsonPropertyName("imei")]
        public string? Imei { get; set; }
        [JsonPropertyName("imei2")]
        public string? Imei2 { get; set; }
        [JsonPropertyName("serial")]
        public string? Serial { get; set; }
        [JsonPropertyName("model")]
        public string? Model { get; set; }
        [JsonPropertyName("androidVersion")]
        public string? AndroidVersion { get; set; }
        [JsonPropertyName("isSimControlLocked")]
        public bool IsSimControlLocked { get; set; }
        [JsonPropertyName("isAppBlockTarget")]
        public string? IsAppBlockTarget { get; set; }
        [JsonPropertyName("blockedAppList")]
        public List<object>? BlockedAppList { get; set; }
        [JsonPropertyName("licenseExpiryDate")]
        public long LicenseExpiryDate { get; set; }
        [JsonPropertyName("firmwareVersion")]
        public string? FirmwareVersion { get; set; }
        [JsonPropertyName("appliedFunctionRestrictions")]
        public object? AppliedFunctionRestrictions { get; set; }
    }
}
