using System.Globalization;

namespace Ranalo.Models
{
    public class DeviceWithDealerDto
    {
        public long DeviceId { get; set; }          // d.Id
        public string? EnrolledOn { get; set; }
        public bool Locked { get; set; }
        public string? LastConnectedAt { get; set; }
        public string? NextLockDateIsoFormat { get; set; }
        public int? DeviceGroupId { get; set; }     // d.DeviceGroupId (nullable if some are null)
        public string ImeiNo { get; set; } = string.Empty; // d.ImeiNo
        public string Make { get; set; } = string.Empty;   // d.Make
        private string _createdAtRaw = string.Empty;

        public string CreatedAt
        {
            get => _createdAtRaw;
            set
            {
                _createdAtRaw = value;
                CreatedAtDate = ParseDate(value);
            }
        }
        public DateTime CreatedAtDate { get; set; }     // d.CreatedAt

        public string DealerId { get; set; } = string.Empty;   // dealer.DealerReference
        public string DealerName { get; set; } = string.Empty; // dealer.CompanyName
        private DateTime ParseDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return DateTime.MinValue;

            // Expected format: "31-10-24 15:05:25 UTC"
            var format = "dd-MM-yy HH:mm:ss 'UTC'";
            if (DateTime.TryParseExact(raw, format, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return parsed;
            }

            // fallback
            return DateTime.MinValue;
        }
    }
}
