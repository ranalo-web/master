using System.Text.Json.Serialization;

namespace Ranalo.SumsungKnox.Models
{
    public class ListDevicesRequest
    {
        [JsonPropertyName("filter")]
        public DeviceListFilter? Filter { get; set; }
        [JsonPropertyName("pageNum")]
        public int PageNum { get; set; }
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }
        [JsonPropertyName("sortBy")]
        public string? SortBy { get; set; } = "updateTime";
        [JsonPropertyName("sortOrder")]
        public string? SortOrder { get; set; } = "descending";
        [JsonPropertyName("search")]
        public string? Search { get; set; }
    }

    public class DeviceListFilter
    {
        [JsonPropertyName("status")]
        public List<string>? Status { get; set; }
        [JsonPropertyName("simControlEnabled")]
        public bool? SimControlEnabled { get; set; }
        [JsonPropertyName("simControlApplied")]
        public bool? SimControlApplied { get; set; }
        [JsonPropertyName("updateTimeFrom")]
        public long? UpdateTimeFrom { get; set; } // Unix timestamp
    }
}
