namespace Ranalo.Models
{
    public class DashboardTotals
    {
        public int DealerId { get; set; }
        public decimal TotalToday { get; set; }
        public int CountToday { get; set; }
        public decimal TotalYesterday { get; set; }
        public int CountYesterday { get; set; }
        public decimal TotalThisWeek { get; set; }
        public int CountThisWeek { get; set; }
        public decimal TotalLastWeek { get; set; }
        public int CountLastWeek { get; set; }
        public decimal TotalThisMonth { get; set; }
        public int CountThisMonth { get; set; }
        public decimal TotalLastMonth { get; set; }
        public int CountLastMonth { get; set; }
        public decimal TotalThisWeekSoFar { get; set; }
        public int CountThisWeekSoFar { get; set; }
        public int TotalOrders { get; set; }
        public int TotalDevices { get; set; }
        public int TotalUsers { get; set; }
        public int TotalTransactions { get; set; }
        public List<CustomerDetails>? Customers { get; set; }
    }
}
