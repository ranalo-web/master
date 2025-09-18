namespace Ranalo.Models
{
    public class RestructuredRecord
    {
        public int Id { get; set; }                // Unique record ID
        public long AccountNo { get; set; }

        public DateTime DateAgreed { get; set; }   // e.g. 2025-02-20

        public decimal AmountRes { get; set; }     // e.g. 62

        public int DaysRestructured { get; set; }

        public decimal TotalDueR { get; set; }

        public decimal TotalPaidR { get; set; }

        public decimal ArrearsR { get; set; }

        public DateTime AutoLockDatePmtR { get; set; }
    }
}
