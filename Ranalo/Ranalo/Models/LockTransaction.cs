namespace Ranalo.Models
{
    public class LockTransaction
    {
        public long AccountId { get; set; }
        public string FirstName { get; set; }
        public decimal UnitsLeft { get; set; }
        public decimal NewDaily { get; set; }
        public DateTime AutoLockDate { get; set; }
        public DateTime LockDate { get; set; }
        public string? Result { get; set; }
    }
}
