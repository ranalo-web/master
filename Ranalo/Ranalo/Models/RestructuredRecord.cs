namespace Ranalo.Models
{
    public class RestructuredRecord
    {
        public int Id { get; set; }                // Unique record ID
        public string FirstName { get; set; }
        public long AccountNo { get; set; }

        public DateTime DateAgreed { get; set; }   // e.g. 2025-02-20

        public decimal AmountRes { get; set; }     // e.g. 62

        public int DaysRestructured { get; set; }

        public decimal TotalDueR { get; set; }

        public decimal TotalPaidR { get; set; }

        public decimal ArrearsR { get; set; }
        public decimal Arrears { get; set; }
        public DateTime? FirstResPaymentDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }

        public DateTime? LastResPaymentDate { get; set; }
        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
        public string? LastConnectedAt { get; set; }
        public decimal LastPaidAmount { get; set; }
        public string? NextLockDate { get; set; }
        public DateTime AutoLockDatePmtR { get; set; }
    }
}
