namespace Ranalo.Models
{
    public class WooOrderProduct
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductColor { get; set; }
        public string? ProductRam { get; set; }
        public string? ProductStorage { get; set; }
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
