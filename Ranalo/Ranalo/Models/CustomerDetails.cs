namespace Ranalo.Models
{
    public class CustomerDetails 
    {
        public int OrderID { get; set; }
        public string Status { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }
        public decimal TotalAmount { get; set; }
        public string CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address1 { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string IMEI { get; set; }
        public string NationalId { get; set; }
        public string DealerRef { get; set; }
        public string CustPhone { get; set; }
        public string CustEmail { get; set; }
        public string MpesaDepositRef { get; set; }
        public string Url { get; set; }

        public List<ImagesMetadata> IdentityImages { get; set; } = new List<ImagesMetadata>();
        public AccountSummary? Summary { get; set; }
    }
}
