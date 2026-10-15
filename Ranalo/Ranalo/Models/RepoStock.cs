namespace Ranalo.Models
{
    // Repo stock (admin only): phones taken back and waiting to be sold
    // again. See Services/RepoStockRules.cs and Database/RepoStock/001.

    public static class RepoStockStatus
    {
        public const string InStock = "InStock";
        public const string Sold = "Sold";

        public static string Label(string status) => status switch
        {
            InStock => "In stock",
            _ => status,
        };
    }

    public static class RepoStockReason
    {
        public const string Repossessed = "Repossessed";
        public const string Returned = "Returned";
        public const string Other = "Other";

        public static readonly string[] All = { Repossessed, Returned, Other };

        public static string Label(string reason) => reason switch
        {
            Returned => "Returned by customer",
            _ => reason,
        };
    }

    public class RepoStockItem
    {
        public int Id { get; set; }
        public long AccountNo { get; set; }
        public string? Imei { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? LockGroup { get; set; }
        public int OldContractId { get; set; }
        public string? OldCustomerName { get; set; }
        public string PaymentSuffix { get; set; } = "_R";
        public string Reason { get; set; } = RepoStockReason.Repossessed;
        public string? Condition { get; set; }
        public string Status { get; set; } = RepoStockStatus.InStock;
        public DateTime IntakeDate { get; set; }
        public string? IntakeByName { get; set; }
        public int? NewContractId { get; set; }
        public string? NewCustomerName { get; set; }
        public string? NewCustomerPhone { get; set; }
        public string? NewCustomerIdNo { get; set; }
        public DateTime? SoldDate { get; set; }
        public string? SoldByName { get; set; }
        public string? SoldDealerName { get; set; }
        public decimal? WriteOffAmount { get; set; }

        // Live: the device's locker now.
        public int? CurrentLockGroup { get; set; }

        public string OldPaymentAccount => $"{AccountNo}{PaymentSuffix}";
        public int DaysInStock => (int)Math.Floor(((SoldDate ?? DateTime.UtcNow) - IntakeDate).TotalDays);
    }

    // What the add form shows for an account before it goes into stock.
    public class RepoStockLookup
    {
        public long AccountNo { get; set; }
        public int? ContractId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? ContractEndedOn { get; set; }   // already ended elsewhere: kept as it is
        public string? Imei { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? LockGroup { get; set; }
        public int? DeviceGroupId { get; set; }
        public string? DealerName { get; set; }
        public int? OpenCollectionsCaseId { get; set; }
        public int TimesInStockBefore { get; set; }
        public bool InStockNow { get; set; }
    }

    // The old contract's figures, for its write-off at sale.
    public class RepoOldContract
    {
        public int ContractId { get; set; }
        public decimal ContractValue { get; set; }
        public decimal TotalPaid { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public decimal? BuyingPrice { get; set; }
    }

    public class RepoPayment
    {
        public string MpesaCode { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    public class RepoSale
    {
        public string CustomerName { get; set; } = "";
        public string? CustomerPhone { get; set; }
        public string? CustomerIdNo { get; set; }
        public decimal Deposit { get; set; }
        public decimal Daily { get; set; }
        public decimal TermInMonths { get; set; } = 12;
        public decimal? BuyingPrice { get; set; }
        public int DealerReference { get; set; }
        public int? AgentUserId { get; set; }
    }

    public class RepoStockListViewModel
    {
        public string Status { get; set; } = "instock";   // instock | sold | all
        public List<RepoStockItem> Items { get; set; } = new();
    }

    public class RepoStockDetailViewModel
    {
        public RepoStockItem Item { get; set; } = null!;
        public RepoOldContract? OldContract { get; set; }
        public List<RepoPayment> PreviousPayments { get; set; } = new();   // one page, newest first
        public int PaymentsPage { get; set; } = 1;
        public int PaymentsPageSize { get; set; } = 10;
        public int PaymentsTotal { get; set; }
        public int PaymentsPages => Math.Max(1, (int)Math.Ceiling(PaymentsTotal / (double)PaymentsPageSize));
        public List<Ranalo.DataStore.Dealer> Dealers { get; set; } = new();
    }

    public record RepoStockResult(bool Success, string Message, int? Id = null);
}
