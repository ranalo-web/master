namespace Ranalo.Woocommece.Api.Models
{
    // The rest of a Safaricom C2B callback, saved onto the KosePayments row
    // for MpesaCode (Database/Payments/002_add_kosepayments_c2b_details.sql).
    public class C2BPaymentDetails
    {
        public required string MpesaCode { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Msisdn { get; set; }
        public string? TransactionType { get; set; }
        public string? BusinessShortCode { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? OrgAccountBalance { get; set; }
        public string? ThirdPartyTransID { get; set; }
    }
}
