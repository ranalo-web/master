namespace Ranalo.Models
{
    public class CustomerDashboardViewModel
    {
        public string CustomerName { get; set; } = "";
        public string ContractNumber { get; set; } = "";
        public string DeviceModel { get; set; } = "";
        public string DeviceStorage { get; set; } = "";
        public string DeviceColor { get; set; } = "";
        public string Imei { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string DealerName { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string Status { get; set; } = "";

        public decimal TotalLoanAmount { get; set; }
        public string ContractStart { get; set; } = "";
        public string ContractEnd { get; set; } = "";
        public string RestructureDate { get; set; } = "";

        public decimal DailyPayment { get; set; }
        public decimal MonthlyInstallment { get; set; }
        public string NextPaymentDue { get; set; } = "";
        public string NextPaymentDaysAway { get; set; } = "";
        public string LastLockDate { get; set; } = "";
        public string NextLockDate { get; set; } = "";

        public decimal PaidToDate { get; set; }
        public decimal BalanceRemaining { get; set; }
        public decimal PercentComplete { get; set; }
        public int InstallmentsPaid { get; set; }
        public int InstallmentsTotal { get; set; }

        public List<CustomerPayment> RecentPayments { get; set; } = new();
    }

    public class CustomerPayment
    {
        public string DatePaid { get; set; } = "";
        public decimal Amount { get; set; }
        public string Method { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal BalanceAfter { get; set; }
    }
}
