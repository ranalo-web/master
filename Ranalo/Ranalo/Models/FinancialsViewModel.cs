namespace Ranalo.Models
{
    public class FinancialsViewModel
    {
        // This month's Income Statement inputs -- same fields/formulas the
        // Admin Dashboard used before this section moved here. Derived
        // figures (Gross/Operating Profit, EBT, Tax, Net Profit, ratios)
        // are computed in the view, same convention as the rest of this app.
        public decimal RevenueThisMonth { get; set; }
        public decimal CostOfDevicesThisMonth { get; set; }
        public decimal CommissionsPaidThisMonth { get; set; }
        public decimal BadDebtThisMonth { get; set; }
        public decimal OperatingExpensesThisMonth { get; set; }
        public decimal TaxRatePct { get; set; }
        public decimal DividendsPaidThisMonth { get; set; }

        // Monthly comparison chart -- last 12 months, oldest first.
        public List<FinancialsMonthRow> MonthlyTrend { get; set; } = new();

        // Balance sheet (best-effort -- see FinancialsMonthRow's doc comment
        // on the site this app has no cash/bank or inventory ledger).
        public decimal LoanReceivablesGross { get; set; }
        public decimal CommissionsPayableToDealers { get; set; }
        public decimal CommissionsPayableToAgents { get; set; }
        public decimal RetainedEarningsAllTime { get; set; }
    }

    // One calendar month's flow figures for the monthly comparison chart.
    // Deliberately excludes Bad Debt and Tax -- both are point-in-time
    // classifications with no reliable "as of a past month" recompute
    // available yet (same limitation the dashboard's own ArrearsChangePct
    // already works around for a single prior month, not a full series) --
    // so NetBeforeTax is Revenue less direct costs only, clearly a
    // pre-tax/pre-bad-debt figure, not the same Net Profit shown elsewhere.
    public class FinancialsMonthRow
    {
        public string MonthLabel { get; set; } = "";
        public decimal Revenue { get; set; }
        public decimal CostOfDevices { get; set; }
        public decimal CommissionsPaid { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal NetBeforeTax { get; set; }

        // Drives the "no expenses logged this month" flag -- true whenever
        // OperatingExpenses > 0 for that month.
        public bool HasOperatingExpenses { get; set; }
    }
}
