namespace Ranalo.Models
{
    // Admin Dashboard "Portfolio Snapshot" profit figures for one period -- the
    // same lines and rules as the Financials Income Statement: cash in, less
    // device cost (at contract start), commissions paid, operating expenses
    // and 30% tax on a profit.
    public class AdminProfit
    {
        public decimal Revenue { get; set; }
        public decimal CostOfDevices { get; set; }
        public decimal Commissions { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal TaxRatePct { get; set; } = 30m;

        public decimal TotalCosts => CostOfDevices + Commissions + OperatingExpenses;
        public decimal ProfitBeforeTax => Revenue - TotalCosts;
        public decimal Tax => Math.Max(0, ProfitBeforeTax) * TaxRatePct / 100;
        public decimal NetProfit => ProfitBeforeTax - Tax;
        public decimal MarginPct => Revenue > 0 ? Math.Round(NetProfit / Revenue * 100, 1) : 0;

        // The biggest of the three costs, for the "what to do" list.
        public (string Name, decimal Amount) BiggestCost => new[]
        {
            ("Device cost", CostOfDevices),
            ("Commissions", Commissions),
            ("Operating expenses", OperatingExpenses),
        }.OrderByDescending(c => c.Item2).First();
    }

    // One "what to do" line on the Admin Dashboard. Level is a Bootstrap
    // colour: danger | warning | success | info.
    public class AdminAction
    {
        public string Level { get; set; } = "info";
        public string Text { get; set; } = "";
        public string? Link { get; set; }
        public string? LinkText { get; set; }

        public int SortOrder => Level switch { "danger" => 0, "warning" => 1, "success" => 2, _ => 3 };
        public string Icon => Level switch
        {
            "danger" => "ni-alert-circle-fill",
            "warning" => "ni-alert-fill",
            "success" => "ni-check-circle-fill",
            _ => "ni-info-fill",
        };
    }
}
