using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ranalo.Woocommece.Api.Services
{
    // Contract term and pricing from a WooCommerce order (agreed 2026-10-14).
    // Kept free of I/O so it can be unit tested.
    //
    // A product variation can carry the "Repayment Period" attribute
    // (pa_repayment-period: "12 Months" / "14 Months"); it sets the contract's
    // Term_in_Months. Each term has its own daily rate and deposit, which the
    // website puts on the line item as _rc_daily and _rc_deposit -- the
    // contract uses those as quoted -- WooCommerce's figures always come
    // first, for every product. Orders without the attribute (TVs, laptops,
    // older orders) are 12 months, as before.
    public static class RepaymentTerms
    {
        public const decimal DefaultTermInMonths = 12m;
        public const string AttributeKey = "pa_repayment-period";
        public const string AttributeName = "Repayment Period";

        // "14 Months", "14-months", "14" -> 14. Null when missing or unreadable.
        public static decimal? ParseTerm(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var match = Regex.Match(value, @"\d+");
            if (!match.Success || !int.TryParse(match.Value, out var months)) return null;
            return months is >= 1 and <= 60 ? months : null;
        }

        // The Repayment Period on a line item, matched by the attribute's key
        // (value is the slug, e.g. "14-months") or its display name.
        public static decimal? ReadTerm(JToken item)
        {
            var meta = item["meta_data"] as JArray;
            if (meta == null) return null;

            var entry = meta.FirstOrDefault(m => m["key"]?.ToString() == AttributeKey)
                        ?? meta.FirstOrDefault(m => string.Equals(m["display_key"]?.ToString(), AttributeName, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return null;

            return ParseTerm(entry["display_value"]?.ToString()) ?? ParseTerm(entry["value"]?.ToString());
        }

        public static decimal? ReadDecimalMeta(JToken item, string key)
        {
            var token = (item["meta_data"] as JArray)?.FirstOrDefault(m => m["key"]?.ToString() == key)?["value"];
            if (token == null || token.Type == JTokenType.Null) return null;
            return decimal.TryParse(token.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
        }

        public static decimal TermOrDefault(decimal? termInMonths) =>
            termInMonths is > 0 ? termInMonths.Value : DefaultTermInMonths;

        // Deposit for a contract with a daily sale price: the website's
        // deposit when the order has one; otherwise the old 12-month
        // working-back from the total (total - daily x 365).
        public static decimal SalesDeposit(decimal totalAmount, decimal dailyRate, decimal? wooDeposit) =>
            wooDeposit is > 0 ? Math.Round(wooDeposit.Value, 2) : totalAmount - (dailyRate * 365);
    }
}
