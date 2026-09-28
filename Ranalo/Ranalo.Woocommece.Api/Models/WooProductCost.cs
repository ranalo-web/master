namespace Ranalo.Woocommece.Api.Models
{
    // A WooCommerce product's (or one variation's) current "Cost of goods".
    // VariationId is null for the product itself; Attributes holds the
    // variation's options (e.g. "8GB", "256GB") for matching an order's
    // RAM/storage.
    public class WooProductCost
    {
        public long ProductId { get; set; }
        public long? VariationId { get; set; }
        public string Name { get; set; } = "";
        public List<string> Attributes { get; set; } = new();
        public decimal? Cost { get; set; }
    }
}
