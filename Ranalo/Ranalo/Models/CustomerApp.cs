namespace Ranalo.Models
{
    // Database/Customers/001: a customer's app access.
    public class CustomerAccess
    {
        public long AccountNo { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? NationalId { get; set; }
        public int FailedCodes { get; set; }
        public DateTime? LastFailedUtc { get; set; }
        public DateTime? LockedAtUtc { get; set; }
        public string? LockedReason { get; set; }
        public string SessionStamp { get; set; } = "";
        public DateTime? LastLoginUtc { get; set; }

        public bool Locked => LockedAtUtc.HasValue;
    }

    // A row on the staff Customer App page.
    public class CustomerAccessRow
    {
        public long AccountNo { get; set; }
        public string? FirstName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int FailedCodes { get; set; }
        public DateTime? LockedAtUtc { get; set; }
        public string? LockedReason { get; set; }
        public DateTime? LastLoginUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UnlockedAtUtc { get; set; }
        public string? UnlockedBy { get; set; }
        public string? UnlockNote { get; set; }
    }

    // A message sent to a phone, to email to its customer as well.
    public class MessageToEmail
    {
        public long AccountNo { get; set; }
        public string Email { get; set; } = "";
        public string? MessageType { get; set; }
        public string? Message { get; set; }
        public string? MessageStatus { get; set; }
        public DateTime DateSent { get; set; }

        public MessageLog AsLog() => new()
        {
            AccountNo = AccountNo.ToString(), MessageType = MessageType, Message = Message,
            MessageStatus = MessageStatus, DateSent = DateSent,
        };
    }

    public class CustomerLoginCode
    {
        public int Id { get; set; }
        public long AccountNo { get; set; }
        public string CodeHash { get; set; } = "";
        public DateTime SentAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool Sent { get; set; }
        public DateTime? UsedAtUtc { get; set; }
    }

    // The phone a code can go to: the account's device and its provider.
    public class CustomerPhoneTarget
    {
        public long AccountNo { get; set; }
        public string? FirstName { get; set; }
        public string? Imei { get; set; }
        public int LockGroup { get; set; }
        // The provider's own lock date (Devices), UTC "dd/MM/yyyy'T'HH:mm:ss".
        // Not the report's NextLockDateIsoFormat, which for accounts in
        // arrears is overwritten with an estimate that's usually in the past.
        public string? DeviceNextLock { get; set; }
        // What the order holds, to fill in the details the first time.
        public string? OrderEmail { get; set; }
        public string? OrderPhone { get; set; }
        public string? OrderNationalId { get; set; }
    }
}
