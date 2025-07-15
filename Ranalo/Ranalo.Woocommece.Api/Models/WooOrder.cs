namespace Ranalo.Woocommece.Api.Models
{
    public class WooOrder
    {
        public int Id { get; set; }
        public long OrderID { get; set; }
        public string? Status { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
        public decimal TotalAmount { get; set; }
        public long CustomerId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Address1 { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? IMEI { get; set; }
        
        public string? NationalId { get; set; }
        public string? DOB { get; set; }
        public string? DealerRef { get; set; }
        public string? CustPhone { get; set; }
        public string? CustEmail { get; set; }
        public DateTime DateSynced { get; set; }
        public string? MpesaDepositRef { get; set; }
        public List<OrderProduct>? Products { get; set; }
        public List<ImagesMetadata>? ImagesMetadata { get; set; }
    }
}
