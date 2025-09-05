namespace Ranalo.Services
{
    public static class DateHelper
    {

        public static DateTime? ParseCustomDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                return null;

            // Remove UTC if present
            dateStr = dateStr.Replace(" UTC", "").Trim();

            // Possible formats the date might have
            string[] formats =
            {
        "dd-MM-yy HH:mm:ss",     // 17-07-25 09:05:08
        "dd-MM-yyyy HH:mm:ss",   // 17-07-2025 09:05:08
        "dd/MM/yy HH:mm:ss",     // 17/07/25 09:05:08
        "dd/MM/yyyy HH:mm:ss"    // 17/07/2025 09:05:08
    };

            // Try parsing against multiple formats
            if (DateTime.TryParseExact(
                    dateStr,
                    formats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out DateTime parsedDate))
            {
                return parsedDate; // ✅ Parsed successfully
            }

            // If still not valid, try general parse as fallback
            if (DateTime.TryParse(dateStr, out parsedDate))
                return parsedDate;

            // ❌ Couldn’t parse
            return null;
        }
    }
}
