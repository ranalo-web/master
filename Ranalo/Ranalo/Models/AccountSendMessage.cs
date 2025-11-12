namespace Ranalo.Models
{
    public class AccountSendMessage
    {
        public long AccountId { get; set; }
        public string FirstName { get; set; }
        public decimal UnitsLeft { get; set; }
        public DateTime AutoLockDatePmtR { get; set; }
    }
}
