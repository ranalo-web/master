namespace Ranalo.Models
{
    // Completed contracts (admin only): closed contracts and how they ended.
    // See Services/CompletedContractRules.cs.

    public enum CompletedOutcome
    {
        PaidOff,      // paid the contract value (a shilling short counts)
        ClosedShort,  // closed with money still owed: written off, repossessed, ended by hand
    }

    public static class CompletedFilter
    {
        public const string PaidOff = "paid";
        public const string ClosedShort = "short";
        public const string All = "all";
    }

    // One closed contract, as read from the database. Paid is what was paid
    // to this contract only: an account's later contract keeps its own
    // payments, and a repo contract's payments are under its renamed account.
    public class CompletedContractRow
    {
        public int ContractId { get; set; }
        public long AccountNo { get; set; }
        public string? CustomerName { get; set; }
        public string? DealerName { get; set; }
        public string? AgentName { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? Imei { get; set; }
        public int? LockGroup { get; set; }
        public string? NuovoStatus { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Deposit { get; set; }
        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
        public decimal TermInMonths { get; set; }
        public decimal? BuyingPrice { get; set; }
        public string? PaymentAccount { get; set; }
        public decimal Paid { get; set; }
        public int PaymentCount { get; set; }
        public DateTime? FirstPaid { get; set; }
        public DateTime? LastPaid { get; set; }
        public string? ReleaseStatus { get; set; }
        public DateTime? ReleaseCompletedAt { get; set; }

        // Filled in by CompletedContractRules.Evaluate.
        public decimal ContractValue { get; set; }
        public CompletedOutcome Outcome { get; set; }
        public int? DaysTaken { get; set; }
    }

    public class CompletedPayment
    {
        public string? MpesaCode { get; set; }
        public decimal Amount { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    public class CompletedContractsViewModel
    {
        public string Filter { get; set; } = CompletedFilter.PaidOff;
        public string? Search { get; set; }
        public Dictionary<string, int> Counts { get; set; } = new();
        public List<CompletedContractRow> Rows { get; set; } = new();
        public int Page { get; set; } = 1;
        public int Pages { get; set; } = 1;
        public int Total { get; set; }
    }

    public class CompletedContractDetailViewModel
    {
        public CompletedContractRow Contract { get; set; } = new();
        public List<CompletedPayment> Payments { get; set; } = new();
        public int PaymentsPage { get; set; } = 1;
        public int PaymentsPages { get; set; } = 1;
    }
}
