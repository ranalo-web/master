namespace Ranalo.Woocommece.Api.Models
{
    public class Contact
    {
        public Guid Id { get; set; }
        public long OrderId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
    }


    public class ContractCreateDto
    {
        public long OrderId { get; set; }
        public string MpesaDepositRef { get; set; }
        public string AccountNo { get; set; }
        public decimal TotalAmount { get; set; }
        public string FirstName { get; set; }
    }
}
