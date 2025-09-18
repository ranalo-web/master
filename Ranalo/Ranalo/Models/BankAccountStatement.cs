namespace Ranalo.Models
{
    public class BankAccountStatement
    {
        public int StatementId { get; set; }

        public long DealerId { get; set; }
        public string AccountName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountType { get; set; }
        public DateTime? GenerationDateTime { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public string GeneratedBy { get; set; }
        public string Currency { get; set; }

        public decimal? AvailableBalance { get; set; }
        public decimal? BalanceAtPeriodStart { get; set; }
        public decimal? BalanceAtPeriodEnd { get; set; }
        public decimal? TotalCredits { get; set; }
        public decimal? TotalDebits { get; set; }
        public string? FileName { get; set; }

        public List<BankTransaction> Transactions { get; set; } = new();
    }

    public class BankTransaction
    {
        public int TransactionId { get; set; }
        public int StatementId { get; set; }
        public DateTime? PostingDate { get; set; }
        public DateTime? ValueDate { get; set; }
        public string BankReference { get; set; }
        public string ChannelReference { get; set; }
        public string TransactionType { get; set; }
        public string TransactionDetails { get; set; }
        public decimal? DebitAmount { get; set; }
        public decimal? CreditAmount { get; set; }
        public decimal? RunningBalance { get; set; }
    }
}
