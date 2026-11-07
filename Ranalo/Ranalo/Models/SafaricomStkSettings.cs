namespace Ranalo.Models
{
    // M-Pesa Express (STK push): the "enter your M-Pesa PIN" prompt sent to
    // the customer's phone for paybill 4090703 (MpesaStkClient,
    // SafaricomStkController). Section "SafaricomStk" in appsettings; the
    // secrets live in Azure only:
    //   SafaricomStk__ConsumerKey, SafaricomStk__ConsumerSecret,
    //   SafaricomStk__Passkey, SafaricomStk__CallbackToken
    // From the Daraja portal app with "M-Pesa Express" (Lipa na M-Pesa
    // Online); Safaricom emails the production passkey when the paybill
    // goes live for it. Off until Enabled is true and all are set.
    public class SafaricomStkSettings
    {
        public bool Enabled { get; set; }

        // https://sandbox.safaricom.co.ke to test, https://api.safaricom.co.ke live.
        public string BaseUrl { get; set; } = "https://api.safaricom.co.ke";

        public string? ConsumerKey { get; set; }
        public string? ConsumerSecret { get; set; }
        public string? Passkey { get; set; }

        // Our paybill (sandbox: 174379).
        public string ShortCode { get; set; } = "4090703";

        // Where Safaricom posts the result: <CallbackBaseUrl>/api/kose/stk/<CallbackToken>/callback
        // (Safaricom refuses URLs containing "mpesa" or "safaricom").
        public string? CallbackBaseUrl { get; set; }
        public string? CallbackToken { get; set; }

        public bool Ready =>
            Enabled
            && !string.IsNullOrWhiteSpace(ConsumerKey)
            && !string.IsNullOrWhiteSpace(ConsumerSecret)
            && !string.IsNullOrWhiteSpace(Passkey)
            && !string.IsNullOrWhiteSpace(CallbackBaseUrl)
            // Shorter than the callback accepts = Safaricom's answers would
            // be refused and paid prompts never recorded (7 Nov 2026), so off.
            && CallbackToken!.Trim().Length >= Ranalo.Services.SafaricomCallerRules.MinTokenLength;
    }

    // One prompt sent, and what became of it (Database/Payments/003).
    public class StkRequest
    {
        public int Id { get; set; }
        public string CheckoutRequestId { get; set; } = "";
        public string? MerchantRequestId { get; set; }
        public long AccountNo { get; set; }
        public string Phone { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = StkStatus.Sent;
        public int? ResultCode { get; set; }
        public string? ResultDesc { get; set; }
        public string? MpesaReceipt { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    public static class StkStatus
    {
        public const string Sent = "Sent";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
    }
}
