namespace Ranalo.Woocommece.Api.Models
{
    public class OrderProduct
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public long ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductColor { get; set; }
        public string? ProductRam { get; set; }
        public string? ProductStorage { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
    }
}
