namespace Ranalo.Models
{
    // A contract with no Contract_Info.BuyingPrice, plus what it was sold
    // as (its WooCommerce order product, or failing that the device's
    // make/model) -- the rows of the device-cost backfill page.
    public class CostBackfillContractRow
    {
        public int ContractId { get; set; }
        public long AccountNo { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? DeviceMake { get; set; }
        public string? DeviceModel { get; set; }
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductRam { get; set; }
        public string? ProductStorage { get; set; }
        // Cost of goods saved on this contract's own order line (orders
        // synced since the cost-of-goods fix).
        public decimal? OrderCost { get; set; }

        public List<CostSuggestion> Suggestions { get; set; } = new();
        public decimal? SelectedCost { get; set; }

        public string DeviceLabel =>
            !string.IsNullOrWhiteSpace(ProductName)
                ? string.Join(" · ", new[] { ProductName, ProductRam, ProductStorage }.Where(s => !string.IsNullOrWhiteSpace(s)))
                : string.Join(" ", new[] { DeviceMake, DeviceModel }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim() is { Length: > 0 } label ? label : "Unknown device";
    }

    // A contract that already has a BuyingPrice -- the "similar devices"
    // source for suggestions.
    public class CostBackfillKnownCost
    {
        public int ContractId { get; set; }
        public DateTime? StartDate { get; set; }
        public decimal BuyingPrice { get; set; }
        public string? DeviceMake { get; set; }
        public string? DeviceModel { get; set; }
        public long? ProductId { get; set; }
        public string? ProductRam { get; set; }
        public string? ProductStorage { get; set; }
    }

    public class CostSuggestion
    {
        // "order" | "woo" | "nearest" | "latest" | "average"
        public string Source { get; set; } = "";
        public string Label { get; set; } = "";
        public decimal Cost { get; set; }
    }

    public class CostBackfillDeviceRow
    {
        public long DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? CustomerName { get; set; }
        public string? DealerName { get; set; }
        // Devices.CreatedAt is stored as text (the API's ISO timestamp).
        public string? CreatedAt { get; set; }

        public string CreatedLabel =>
            DateTime.TryParse(CreatedAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
                ? d.ToString("dd MMM yyyy")
                : CreatedAt ?? "";
    }

    public class CostBackfillViewModel
    {
        // Which suggestion pre-fills each row's cost -- see CostSuggestion.Source.
        public string Rule { get; set; } = "woo";
        public List<CostBackfillContractRow> Contracts { get; set; } = new();
        public List<CostBackfillDeviceRow> DevicesWithoutContracts { get; set; } = new();
        public string? WooError { get; set; }

        public int WithSuggestion => Contracts.Count(c => c.Suggestions.Any());
        public int PreFilled => Contracts.Count(c => c.SelectedCost.HasValue);
    }

    // POST model for applying the chosen costs.
    public class CostBackfillApplyRow
    {
        public int ContractId { get; set; }
        public bool Include { get; set; }
        public decimal? BuyingPrice { get; set; }
    }
}
