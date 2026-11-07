using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Ranalo.Services
{
    // M-Pesa Express (STK push) rules, kept apart from the HTTP calls so
    // they can be tested (MpesaStkClient, SafaricomStkController).
    public static class StkRules
    {
        // Safaricom's limits on the request text.
        public const int AccountReferenceMax = 12;
        public const int DescriptionMax = 13;

        // 2547XXXXXXXX / 2541XXXXXXXX, from 07.., 01.., 7.., +254.., 254..
        // Anything else is refused (null) rather than sent to a stranger.
        public static string? Phone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var digits = new string(raw.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("254")) digits = digits[3..];
            else if (digits.StartsWith("0")) digits = digits[1..];

            return digits.Length == 9 && (digits[0] == '7' || digits[0] == '1') ? "254" + digits : null;
        }

        // Whole shillings, at least 1: M-Pesa takes no cents.
        public static int? Amount(decimal amount)
        {
            var whole = (int)Math.Ceiling(amount);
            return whole >= 1 && whole <= 250_000 ? whole : null;
        }

        // ---------- Who may be prompted, and how often (agreed 2026-11-07) ----------
        // Anyone can pay for the account from any Safaricom number (family
        // pays too), but within limits so the prompt can't be used to pester
        // or press someone: counted over the last 24 hours, from every
        // prompt sent (StkRequests).
        public const int MaxPerHour = 3;
        public const int MaxPerDay = 6;
        public const int MaxOtherNumbersPerDay = 2;
        public const int MaxAccountsPerNumberPerDay = 2;
        public static readonly TimeSpan MinGap = TimeSpan.FromSeconds(60);

        public record Sent(long AccountNo, string Phone, DateTime CreatedAtUtc);

        // null = may send. recent = prompts in the last 24 hours from this
        // account or to this phone. savedPhone = the customer's own M-Pesa
        // number (their Details); balance = what's left to pay.
        public static string? Refusal(long accountNo, string phone, string? savedPhone, int amount, decimal? balance,
            IReadOnlyCollection<Sent> recent, DateTime nowUtc)
        {
            var dayAgo = nowUtc.AddDays(-1);
            var mine = recent.Where(r => r.AccountNo == accountNo && r.CreatedAtUtc > dayAgo).ToList();

            if (balance is > 0 && amount > Math.Ceiling(balance.Value))
                return $"That's more than what's left to pay (Ksh {Math.Ceiling(balance.Value):#,##0}). Enter Ksh {Math.Ceiling(balance.Value):#,##0} or less.";

            var last = mine.Select(r => r.CreatedAtUtc).DefaultIfEmpty().Max();
            if (last != default && nowUtc - last < MinGap)
                return "A prompt was just sent. Check the phone, or wait a minute and try again.";

            if (mine.Count(r => r.CreatedAtUtc > nowUtc.AddHours(-1)) >= MaxPerHour)
                return $"That's {MaxPerHour} prompts in the last hour. Try again later, or pay with Pay Bill {CustomerMessages.Paybill}.";

            if (mine.Count >= MaxPerDay)
                return $"That's {MaxPerDay} prompts today. Pay with Pay Bill {CustomerMessages.Paybill}, or try again tomorrow.";

            var own = Phone(savedPhone);
            if (phone != own)
            {
                var others = mine.Select(r => r.Phone).Where(p => p != own).Distinct().ToList();
                if (!others.Contains(phone) && others.Count >= MaxOtherNumbersPerDay)
                    return $"Prompts can go to {MaxOtherNumbersPerDay} other numbers a day. Use your own number, or Pay Bill {CustomerMessages.Paybill}.";
            }

            var accountsForPhone = recent.Where(r => r.Phone == phone && r.CreatedAtUtc > dayAgo).Select(r => r.AccountNo).Distinct().ToList();
            if (!accountsForPhone.Contains(accountNo) && accountsForPhone.Count >= MaxAccountsPerNumberPerDay)
                return $"That number has had prompts for other accounts today. Pay with Pay Bill {CustomerMessages.Paybill} instead.";

            return null;
        }

        // Kenya time, which Safaricom checks against the password.
        public static string Timestamp(DateTime nowUtc) =>
            DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc).AddHours(3).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        public static string Password(string shortCode, string passkey, string timestamp) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(shortCode + passkey + timestamp));

        public static string AccountReference(long accountNo)
        {
            var text = accountNo.ToString(CultureInfo.InvariantCulture);
            return text.Length > AccountReferenceMax ? text[..AccountReferenceMax] : text;
        }

        // What Safaricom posts back once the customer has answered (or not).
        public record Result(string CheckoutRequestId, int ResultCode, string? ResultDesc,
            decimal? Amount, string? Receipt, DateTime? PaidAtKenya, string? Phone)
        {
            public bool Paid => ResultCode == 0 && !string.IsNullOrWhiteSpace(Receipt) && Amount > 0;
        }

        // { "Body": { "stkCallback": { "CheckoutRequestID", "ResultCode", "ResultDesc",
        //   "CallbackMetadata": { "Item": [ { "Name": "Amount", "Value": 1 }, ... ] } } } }
        public static Result? ReadCallback(JsonElement body)
        {
            if (body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("Body", out var b) || b.ValueKind != JsonValueKind.Object
                || !b.TryGetProperty("stkCallback", out var cb) || cb.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var checkout = Text(cb, "CheckoutRequestID");
            if (string.IsNullOrWhiteSpace(checkout) || !cb.TryGetProperty("ResultCode", out var rc)) return null;

            var resultCode = rc.ValueKind == JsonValueKind.Number ? rc.GetInt32()
                : int.TryParse(rc.GetString(), out var parsed) ? parsed : -1;

            var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (cb.TryGetProperty("CallbackMetadata", out var meta) && meta.ValueKind == JsonValueKind.Object
                && meta.TryGetProperty("Item", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    var name = Text(item, "Name");
                    if (name != null && item.TryGetProperty("Value", out var v))
                    {
                        items[name] = v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText();
                    }
                }
            }

            decimal? amount = items.TryGetValue("Amount", out var a)
                && decimal.TryParse(a, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) ? amt : null;
            DateTime? paidAt = items.TryGetValue("TransactionDate", out var d)
                && DateTime.TryParseExact(d, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

            return new Result(checkout.Trim(), resultCode, Text(cb, "ResultDesc"), amount,
                items.TryGetValue("MpesaReceiptNumber", out var r) ? r.Trim().ToUpperInvariant() : null,
                paidAt, items.TryGetValue("PhoneNumber", out var p) ? p : null);
        }

        private static string? Text(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
