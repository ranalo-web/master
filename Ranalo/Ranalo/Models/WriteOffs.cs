namespace Ranalo.Models
{
    // One WriteOffs register row (Database/WriteOffs/001) with what the
    // review screen needs to decide on it.
    public class WriteOffRow
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public long AccountNo { get; set; }
        public string? CustomerName { get; set; }
        public string? DealerName { get; set; }
        public string? DeviceName { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public string Reason { get; set; } = "";
        public DateTime WrittenOffDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public decimal ContractValue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public string Status { get; set; } = "";
        public string? HoldReason { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReinstatedDate { get; set; }
        public string? ReinstatedMpesaCode { get; set; }

        // Real-loss inputs.
        public decimal? BuyingPrice { get; set; }
        public decimal CommissionsPaid { get; set; }
        public decimal RepossessionCost { get; set; }
        public decimal ResaleValue { get; set; }

        // What this bad account actually cost the business: device cost +
        // commissions + repossession cost - what the customer paid - resale.
        // Null when the device's buying price was never recorded (see the
        // Device Cost Backfill page).
        public decimal? RealLoss => BuyingPrice.HasValue
            ? BuyingPrice.Value + CommissionsPaid + RepossessionCost - TotalPaid - ResaleValue
            : null;

        public string ReasonLabel => Reason == "Repossessed" ? "Repossessed" : "360 days no payment";

        public string DisplayStatus => ReinstatedDate.HasValue ? "Reinstated" : Status;
    }

    public class WriteOffStatusSummary
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
        public decimal Outstanding { get; set; }
    }

    // Financials: write-offs dated in / reinstated in the reporting period.
    public class WriteOffPeriodSummary
    {
        public int WrittenOffCount { get; set; }
        public decimal WrittenOffBalance { get; set; }
        public decimal RealLoss { get; set; }
        public int MissingCostCount { get; set; }
        public int ReinstatedCount { get; set; }
        public decimal ReinstatedBalance { get; set; }
    }

    // Financials age-of-debt report: open contracts still owing money.
    // SortOrder 1-5 are on the loan book by days since last payment; 6 is
    // awaiting write-off review (still on the book); 7 is written off.
    public class LoanBookAgeBucket
    {
        public int SortOrder { get; set; }
        public string Label { get; set; } = "";
        public int Accounts { get; set; }
        public decimal Outstanding { get; set; }

        public bool IsWrittenOff => SortOrder == 7;
    }

    public class WriteOffsViewModel
    {
        // pending | held | approved | reinstated
        public string Status { get; set; } = "pending";
        public string SearchTerm { get; set; } = "";
        public List<WriteOffRow> Rows { get; set; } = new();
        public List<WriteOffStatusSummary> Summary { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalRecords { get; set; }

        public WriteOffStatusSummary For(string status) =>
            Summary.FirstOrDefault(s => s.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            ?? new WriteOffStatusSummary { Status = status };
    }
}
