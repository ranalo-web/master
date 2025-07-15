namespace Ranalo.Models
{
    public class AwaitingApprovalDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public required string Status { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? NationalId { get; set; }
        public string? Address1 { get; set; }
        public string? DealerRef { get; set; }
        public string? MpesaDepositRef { get; set; }
        public string? MpesaCode { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
