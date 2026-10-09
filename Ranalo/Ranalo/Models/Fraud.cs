namespace Ranalo.Models
{
    // Fraud section (admin only). See Services/FraudRules.cs for the checks
    // and Database/Fraud/001 for the review table.

    public static class FraudCheckCodes
    {
        public const string DepositOnly = "DEPOSIT_ONLY";
        public const string NoDevice = "NO_DEVICE";
        public const string LockOverdue = "LOCK_OVERDUE";
        public const string ImeiOnOtherAccount = "IMEI_OTHER_ACCOUNT";
        public const string ImeiReused = "IMEI_REUSED";
        public const string PhoneMultiId = "PHONE_MULTI_ID";
        public const string NextOfKinShared = "NOK_SHARED";
        public const string DepositRefReused = "DEPOSIT_REF_REUSED";
    }

    public static class FraudReviewStatus
    {
        public const string Open = "Open";
        public const string Investigating = "Investigating";
        public const string Cleared = "Cleared";
        public const string Confirmed = "Confirmed";

        public static readonly string[] Settable = { Investigating, Cleared, Confirmed };
    }

    public record FraudCheckInfo(string Code, string Title, string Description, bool Strong);

    // An open contract with what the account checks need.
    public class FraudContractRow
    {
        public long AccountNo { get; set; }
        public int ContractId { get; set; }
        public string? CustomerName { get; set; }
        public int? DealerId { get; set; }
        public string? DealerName { get; set; }
        public long? AgentUserId { get; set; }      // Contract_Info.AssignedAgentId
        public DateTime StartDate { get; set; }
        public decimal Deposit { get; set; }
        public decimal ContractValue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Shortfall { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public bool HasDevice { get; set; }
        public string? DeviceName { get; set; }
        public bool? Locked { get; set; }
        public string? NextLockDate { get; set; }   // Devices.NextLockDate, dd/MM/yyyy
        public string? LastConnectedAt { get; set; }
    }

    // A WooCommerce order that hasn't been cancelled, failed or rejected.
    public class FraudOrderRow
    {
        public long OrderId { get; set; }
        public string? Status { get; set; }
        public DateTime DateCreated { get; set; }
        public string? CustomerName { get; set; }
        public string? NationalId { get; set; }
        public string? Phone { get; set; }
        public string? Imei { get; set; }
        public string? MpesaDepositRef { get; set; }
        public string? DealerRef { get; set; }
        // The order's contract account, else the enrolled account its
        // deposit was paid to -- how dealers see their orders elsewhere.
        public long? AccountNo { get; set; }
        public int? DealerId { get; set; }          // the dealer of that account's device
        public string? DealerName { get; set; }
        public long? AgentUserId { get; set; }      // the account's assigned agent
    }

    public class FraudNextOfKinRow
    {
        public long OrderId { get; set; }
        public string? Phone { get; set; }
        public string? IdNumber { get; set; }
    }

    // A device on an open contract, with the national ID of the order that
    // contract was made from (when the order is linked).
    public class FraudDeviceRow
    {
        public long AccountNo { get; set; }
        public string? Imei { get; set; }
        public string? Imei2 { get; set; }
        public string? CustomerName { get; set; }
        public string? ContractOrderNationalId { get; set; }
    }

    public class FraudReview
    {
        public string CheckCode { get; set; } = "";
        public string SubjectKey { get; set; } = "";
        public string Status { get; set; } = FraudReviewStatus.Open;
        public string? Notes { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
    }

    public class FraudFlag
    {
        public string CheckCode { get; set; } = "";
        public string SubjectKey { get; set; } = "";
        public long? AccountNo { get; set; }
        public List<long> OrderIds { get; set; } = new();
        public string? CustomerName { get; set; }
        public int? DealerId { get; set; }
        public string? DealerName { get; set; }
        public string Evidence { get; set; } = "";
        // When the flag first applied (e.g. the order date, or 14 days after
        // the contract started), for "new this week".
        public DateTime FlaggedOn { get; set; }
        // Contract value not yet paid, for account flags.
        public decimal AmountAtRisk { get; set; }
        public FraudReview? Review { get; set; }
        public string ReviewStatus => Review?.Status ?? FraudReviewStatus.Open;
    }

    public enum FraudRiskLevel { Normal, Watch, High }

    public class FraudDealerRisk
    {
        public int? DealerId { get; set; }
        public string DealerName { get; set; } = "";
        public int Contracts { get; set; }              // open contracts at least 14 days old
        public int DepositOnly { get; set; }
        public decimal DepositOnlyRate => Contracts > 0 ? (decimal)DepositOnly / Contracts : 0m;
        public int LockOverdue { get; set; }
        public int OtherFlags { get; set; }             // every other open flag tied to the dealer
        public int Confirmed { get; set; }
        public decimal AmountAtRisk { get; set; }
        public FraudRiskLevel Level { get; set; }
    }

    public class FraudCheckSummary
    {
        public FraudCheckInfo Check { get; set; } = null!;
        public int Open { get; set; }                   // not cleared
        public int NewThisWeek { get; set; }
        public int Confirmed { get; set; }
        public decimal AmountAtRisk { get; set; }
    }

    // What a dealer or agent sees: never which check fired or why.
    public static class VerificationStatus
    {
        public const string UnderVerification = "Under verification";
        public const string Failed = "Failed – suspected fraud";
    }

    public class VerificationItem
    {
        public long? AccountNo { get; set; }
        public long? OrderId { get; set; }
        public string? CustomerName { get; set; }
        public int? DealerId { get; set; }
        public long? AgentUserId { get; set; }
        public DateTime Since { get; set; }
        public string Status { get; set; } = VerificationStatus.UnderVerification;
    }

    public class VerificationViewModel
    {
        public List<VerificationItem> Items { get; set; } = new();
    }

    public class FraudViewModel
    {
        public string? Check { get; set; }
        public int? DealerId { get; set; }
        public string Status { get; set; } = "active";  // active | open | investigating | confirmed | cleared | all
        public decimal OverallDepositOnlyRate { get; set; }
        public List<FraudCheckSummary> Checks { get; set; } = new();
        public List<FraudDealerRisk> Dealers { get; set; } = new();
        public List<FraudFlag> Flags { get; set; } = new();
    }
}
