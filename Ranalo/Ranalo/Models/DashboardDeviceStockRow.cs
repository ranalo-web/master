namespace Ranalo.Models
{
    // Maps 1:1 onto a row in the DashboardDeviceStock rollup table.
    public class DashboardDeviceStockRow
    {
        public string DeviceName { get; set; } = "";
        public int Units { get; set; }
        public decimal AvgValue { get; set; }
        public decimal GoodPct { get; set; }
        public decimal ArrearsPct { get; set; }
    }
}
