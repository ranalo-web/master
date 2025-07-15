namespace Ranalo.Woocommece.Api.Models
{
    public class Order
    {
      public int OrderId { get; set; }
      public string OrderRef { get; set; }
      public int CustomerId { get; set; }
      public int DealerId { get; set; }
      public int ProductId { get; set; }
      public DateTime OrderDate { get; set; }
      public  required string Status { get; set; }
      public DateTime LastUpdated { get; set; }
      public string? BillingAddress { get; set; }
      public string? ShippingAddress { get; set; }
      public decimal TotalAmount { get; set; }
      public string? ApprovalStatus { get; set; }
      public string? Origin { get; set; }
      public DateTime ApprovalRejectionDate { get; set; }
      public string? IMEINumber { get; set; }
    }
}
