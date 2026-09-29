using Ranalo.Services;

namespace Ranalo.Models
{
    public static class CommissionPayeeType
    {
        public const string Agent = "Agent";
        public const string Dealer = "Dealer";

        public static string? Normalize(string? value) =>
            string.Equals(value, Agent, StringComparison.OrdinalIgnoreCase) ? Agent
            : string.Equals(value, Dealer, StringComparison.OrdinalIgnoreCase) ? Dealer
            : null;
    }

    public static class CommissionPayoutMethod
    {
        public static readonly string[] All = { "M-Pesa", "Bank", "Cash", "Other" };
    }

    // One dealer or agent on the Pay Commissions list.
    public class CommissionPayeeSummary
    {
        public string PayeeType { get; set; } = "";
        public int PayeeId { get; set; }
        public string Name { get; set; } = "";
        public string? DealerName { get; set; }
        public int Accounts { get; set; }
        public int AccountsInArrears { get; set; }
        public decimal DefaultRatePct { get; set; }
        public bool IsSuspended { get; set; }
        public CommissionPool Pool { get; set; } = new();

        // What can be paid today: the pooled Owed, or 0 while suspended.
        public decimal Payable { get; set; }

        // Accounts that matched the search box, if any.
        public List<CommissionMatchedAccount> MatchedAccounts { get; set; } = new();
    }

    public class CommissionMatchedAccount
    {
        public long AccountId { get; set; }
        public long ContractId { get; set; }
        public string CustomerName { get; set; } = "";
    }

    public class CommissionPayeesViewModel
    {
        public string? PayeeTypeFilter { get; set; }
        public string? Search { get; set; }
        public List<CommissionPayeeSummary> Payees { get; set; } = new();
    }

    // One of the payee's accounts on the Pay screen.
    public class CommissionPayAccountRow
    {
        public long ContractId { get; set; }
        public long AccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? AgentName { get; set; }
        public DateTime StartDate { get; set; }
        public int DaysSinceStart { get; set; }
        public bool InArrears { get; set; }
        public bool MissingBuyingPrice { get; set; }
        public decimal Earned { get; set; }
        public decimal Deducted { get; set; }
        public decimal Paid { get; set; }
        public decimal Unpaid { get; set; }
        public decimal OwnNet { get; set; }

        // Expandable detail panel (display only).
        public string? ProductName { get; set; }
        public string? Imei { get; set; }
        public decimal Deposit { get; set; }
        public decimal? BuyingPrice { get; set; }
        public decimal? ContractValue { get; set; }
        public decimal TotalPaid { get; set; }
        public int DaysPastLock { get; set; }
        public CommissionBreakdown Breakdown { get; set; } = new();
        public List<CommissionAccountPayment> Payments { get; set; } = new();
    }

    public class CommissionPayViewModel
    {
        public CommissionPayeeSummary Payee { get; set; } = new();
        public List<CommissionPayAccountRow> Accounts { get; set; } = new();
        public List<CommissionPayoutRecord> RecentPayouts { get; set; } = new();
        public List<long> PreselectedContractIds { get; set; } = new();
    }

    // One payment line on one account.
    public class CommissionAccountPayment
    {
        public long ContractId { get; set; }
        public DateTime? PaidDate { get; set; }
        public decimal Amount { get; set; }
        public string? Method { get; set; }
        public string? Reference { get; set; }
    }

    // A saved payout with its details, for the history list.
    public class CommissionPayoutRecord
    {
        public int Id { get; set; }
        public string PayeeType { get; set; } = "";
        public int? DealerId { get; set; }
        public int? AgentUserId { get; set; }
        public string PayeeName { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime PaidDate { get; set; }
        public string Method { get; set; } = "";
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public string? RecordedByName { get; set; }
        public DateTime RecordedAtUtc { get; set; }
        public DateTime? ReceiptConfirmedAtUtc { get; set; }
        public string? ReceiptConfirmedByName { get; set; }
        public int AccountCount { get; set; }
    }

    public class CommissionPayoutHistoryViewModel
    {
        public bool IsAdminView { get; set; }
        public List<CommissionPayoutRecord> Payouts { get; set; } = new();
    }

    public class NewCommissionPayout
    {
        public string PayeeType { get; set; } = "";
        public int? DealerId { get; set; }
        public int? AgentUserId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaidDate { get; set; }
        public string Method { get; set; } = "";
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public int RecordedByUserId { get; set; }
        public List<(long ContractId, decimal Amount)> Lines { get; set; } = new();
    }
}
