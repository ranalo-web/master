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
        // Next of kin's national ID (form field billing_next_of_kin_id_number[_2]).
        public string? IdNumber { get; set; }
    }


    public class ContractCreateDto
    {
        public long OrderId { get; set; }
        public string MpesaDepositRef { get; set; }
        public string AccountNo { get; set; }
        public decimal TotalAmount { get; set; }
        public string FirstName { get; set; }
        public decimal TermInMonths { get; set; } = 12.00000m;
        public decimal? DailySalePrice { get; set; }
        // Unit cost of goods from the order's product (Woo_OrderProduct),
        // copied onto the new contract's BuyingPrice.
        public decimal? BuyingPrice { get; set; }
    }
}
