using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    // Database/Customers/001: customer app login.
    public interface ICustomerAccessRepository
    {
        Task<CustomerPhoneTarget?> GetTargetAsync(long accountNo);
        Task<CustomerAccess> GetOrCreateAsync(long accountNo, CustomerPhoneTarget? prefill);
        Task<List<DateTime>> CodesSentSinceAsync(long accountNo, DateTime sinceUtc);
        Task AddCodeAsync(CustomerLoginCode code, string? ip);
        Task<CustomerLoginCode?> LatestLiveCodeAsync(long accountNo, DateTime nowUtc);
        Task MarkUsedAsync(int codeId);
        Task<int> RecordFailureAsync(long accountNo);
        Task LockAsync(long accountNo, string reason);
        Task RecordLoginAsync(long accountNo);
        Task SaveDetailsAsync(long accountNo, string? email, string? phone, string? nationalId);
        Task<Ranalo.Services.MyDevice?> GetDeviceAsync(long accountNo);
        Task<int?> GetLatestEndedContractIdAsync(long accountNo);
        Task<List<MessageLog>> GetMessagesAsync(long accountNo, string? imei, int take);
        Task<List<MessageToEmail>> GetMessagesToEmailAsync(int take);
        Task<(int Count, MessageLog? Newest)> GetNewMessagesAsync(long accountNo, string? imei, DateTime sinceUtc);
        Task MarkInboxSeenAsync(long accountNo);
        Task<CustomerAccess?> GetAsync(long accountNo);
        Task<List<CustomerAccessRow>> ListAsync(bool lockedOnly, string? search);
        Task<bool> UnlockAsync(long accountNo, int userId, string note);
        Task SetEmailedUpToAsync(long accountNo, DateTime upToUtc);
        Task<List<Ranalo.Services.MyPayment>> GetPaymentsAsync(long accountNo);
    }

    public class CustomerAccessRepository : ICustomerAccessRepository
    {
        private readonly IDbConnection _db;

        public CustomerAccessRepository(IDbConnection db)
        {
            _db = db;
        }

        // The account's device and provider, plus what its order holds.
        public async Task<CustomerPhoneTarget?> GetTargetAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<CustomerPhoneTarget>(@"
                SELECT d.Id AS AccountNo, CAST(d.ImeiNo AS NVARCHAR(50)) AS Imei, ISNULL(d.LockGroup, 1) AS LockGroup,
                       d.NextLockDateIsoFormat AS DeviceNextLock,
                       ci.First_Name AS FirstName,
                       NULLIF(LTRIM(COALESCE(NULLIF(wo.CustEmail, ''), wo.Email)), '') AS OrderEmail,
                       NULLIF(LTRIM(COALESCE(NULLIF(wo.CustPhone, ''), wo.Phone)), '') AS OrderPhone,
                       NULLIF(LTRIM(wo.NationalId), '') AS OrderNationalId
                FROM Devices d
                OUTER APPLY (SELECT TOP 1 c.First_Name FROM Contract_Info c WHERE c.ID = d.Id
                             ORDER BY CASE WHEN c.EndDate IS NULL THEN 0 ELSE 1 END, c.ContractID DESC) ci
                OUTER APPLY (SELECT TOP 1 o.CustEmail, o.Email, o.CustPhone, o.Phone, o.NationalId
                             FROM KosePayments kp JOIN Woo_Orders o ON o.MpesaDepositRef = kp.MpesaCode
                             WHERE kp.AccountNoBigint = d.Id ORDER BY o.DateCreated DESC) wo
                WHERE d.Id = @AccountNo", new { AccountNo = accountNo });

        // First visit: a row, with the order's details to start from.
        public async Task<CustomerAccess> GetOrCreateAsync(long accountNo, CustomerPhoneTarget? prefill)
        {
            await _db.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM CustomerAccess WHERE AccountNo = @AccountNo)
                    INSERT INTO CustomerAccess (AccountNo, Email, Phone, NationalId)
                    VALUES (@AccountNo, @Email, @Phone, @NationalId)",
                new
                {
                    AccountNo = accountNo,
                    Email = prefill?.OrderEmail,
                    Phone = Ranalo.Services.StkRules.Phone(prefill?.OrderPhone),
                    NationalId = prefill?.OrderNationalId,
                });
            return await _db.QuerySingleAsync<CustomerAccess>("SELECT * FROM CustomerAccess WHERE AccountNo = @AccountNo", new { AccountNo = accountNo });
        }

        public async Task<List<DateTime>> CodesSentSinceAsync(long accountNo, DateTime sinceUtc) =>
            (await _db.QueryAsync<DateTime>(
                "SELECT SentAtUtc FROM CustomerLoginCodes WHERE AccountNo = @AccountNo AND SentAtUtc >= @Since",
                new { AccountNo = accountNo, Since = sinceUtc })).ToList();

        public async Task AddCodeAsync(CustomerLoginCode code, string? ip) =>
            await _db.ExecuteAsync(@"
                INSERT INTO CustomerLoginCodes (AccountNo, CodeHash, SentAtUtc, ExpiresAtUtc, Sent, RequestIp)
                VALUES (@AccountNo, @CodeHash, @SentAtUtc, @ExpiresAtUtc, @Sent, @Ip)",
                new { code.AccountNo, code.CodeHash, code.SentAtUtc, code.ExpiresAtUtc, code.Sent, Ip = ip });

        // Only the newest code counts: asking for a new one cancels the old.
        public async Task<CustomerLoginCode?> LatestLiveCodeAsync(long accountNo, DateTime nowUtc) =>
            await _db.QueryFirstOrDefaultAsync<CustomerLoginCode>(@"
                SELECT TOP 1 * FROM CustomerLoginCodes
                WHERE AccountNo = @AccountNo AND Sent = 1
                ORDER BY SentAtUtc DESC", new { AccountNo = accountNo }) is { } latest
                && latest.UsedAtUtc == null && latest.ExpiresAtUtc > nowUtc ? latest : null;

        public async Task MarkUsedAsync(int codeId) =>
            await _db.ExecuteAsync("UPDATE CustomerLoginCodes SET UsedAtUtc = SYSUTCDATETIME() WHERE Id = @Id AND UsedAtUtc IS NULL", new { Id = codeId });

        // Wrong codes in a row, after this one.
        public async Task<int> RecordFailureAsync(long accountNo) =>
            await _db.ExecuteScalarAsync<int>(@"
                UPDATE CustomerAccess SET FailedCodes = FailedCodes + 1, LastFailedUtc = SYSUTCDATETIME()
                OUTPUT INSERTED.FailedCodes
                WHERE AccountNo = @AccountNo", new { AccountNo = accountNo });

        // Ends every signed-in phone too.
        public async Task LockAsync(long accountNo, string reason) =>
            await _db.ExecuteAsync(@"
                UPDATE CustomerAccess SET LockedAtUtc = SYSUTCDATETIME(), LockedReason = LEFT(@Reason, 200),
                       SessionStamp = CONVERT(NVARCHAR(40), NEWID())
                WHERE AccountNo = @AccountNo AND LockedAtUtc IS NULL", new { AccountNo = accountNo, Reason = reason });

        public async Task RecordLoginAsync(long accountNo) =>
            await _db.ExecuteAsync(@"
                UPDATE CustomerAccess SET FailedCodes = 0, LastFailedUtc = NULL, LastLoginUtc = SYSUTCDATETIME()
                WHERE AccountNo = @AccountNo", new { AccountNo = accountNo });

        public async Task SaveDetailsAsync(long accountNo, string? email, string? phone, string? nationalId) =>
            await _db.ExecuteAsync(@"
                UPDATE CustomerAccess SET Email = @Email, Phone = @Phone, NationalId = @NationalId, UpdatedAtUtc = SYSUTCDATETIME()
                WHERE AccountNo = @AccountNo",
                new { AccountNo = accountNo, Email = email, Phone = phone, NationalId = nationalId });

        // The product on the account's latest order, and its WooCommerce photos.
        // (Woo_OrderProduct.OrderId is Woo_Orders.Id, not OrderID.)
        public async Task<Ranalo.Services.MyDevice?> GetDeviceAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<Ranalo.Services.MyDevice>(@"
                SELECT TOP 1 op.ProductName, p.Images
                FROM KosePayments kp
                JOIN Woo_Orders o ON o.MpesaDepositRef = kp.MpesaCode
                JOIN Woo_OrderProduct op ON op.OrderId = o.Id
                LEFT JOIN Products p ON p.ID = op.ProductId
                WHERE kp.AccountNoBigint = @AccountNo
                ORDER BY o.DateCreated DESC", new { AccountNo = accountNo });

        // Everything we've sent the phone (Nuovo/Transsion log by account,
        // Knox by IMEI), newest first. Never login codes -- and those are
        // logged without the code anyway.
        public async Task<List<MessageLog>> GetMessagesAsync(long accountNo, string? imei, int take) =>
            (await _db.QueryAsync<MessageLog>(@"
                SELECT TOP (@Take) Id, AccountNo, MessageType, Message, DateSent, MessageStatus
                FROM MessageLogs
                WHERE AccountNo IN (@Account, @Imei) AND MessageType <> 'LoginCode'
                ORDER BY DateSent DESC",
                new { Take = take, Account = accountNo.ToString(), Imei = string.IsNullOrWhiteSpace(imei) ? accountNo.ToString() : imei })).ToList();

        // Messages sent to the phones of customers who gave an email, newer
        // than what they've been emailed (or than when they joined the app),
        // oldest first. Not to locked customers; never login codes.
        public async Task<List<MessageToEmail>> GetMessagesToEmailAsync(int take) =>
            (await _db.QueryAsync<MessageToEmail>(@"
                SELECT TOP (@Take) ca.AccountNo, ca.Email, m.MessageType, m.Message, m.MessageStatus, m.DateSent
                FROM CustomerAccess ca
                JOIN Devices d ON d.Id = ca.AccountNo
                JOIN MessageLogs m ON m.AccountNo IN (CAST(ca.AccountNo AS NVARCHAR(50)), CAST(d.ImeiNo AS NVARCHAR(50)))
                WHERE ca.Email IS NOT NULL AND ca.LockedAtUtc IS NULL
                  AND m.MessageType <> 'LoginCode' AND m.MessageStatus = 'sent'
                  AND m.DateSent > COALESCE(ca.EmailedUpToUtc, ca.CreatedAtUtc)
                ORDER BY m.DateSent", new { Take = take })).ToList();

        public async Task<CustomerAccess?> GetAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<CustomerAccess>("SELECT * FROM CustomerAccess WHERE AccountNo = @AccountNo", new { AccountNo = accountNo });

        // Staff page: locked first, then most recent sign-ins. Search by
        // account number, name, email or phone.
        public async Task<List<CustomerAccessRow>> ListAsync(bool lockedOnly, string? search) =>
            (await _db.QueryAsync<CustomerAccessRow>(@"
                SELECT TOP 500 ca.AccountNo, ci.First_Name AS FirstName, ca.Email, ca.Phone, ca.FailedCodes,
                       ca.LockedAtUtc, ca.LockedReason, ca.LastLoginUtc, ca.CreatedAtUtc,
                       ca.UnlockedAtUtc, u.KnownAs AS UnlockedBy, ca.UnlockNote
                FROM CustomerAccess ca
                OUTER APPLY (SELECT TOP 1 c.First_Name FROM Contract_Info c WHERE c.ID = ca.AccountNo
                             ORDER BY CASE WHEN c.EndDate IS NULL THEN 0 ELSE 1 END, c.ContractID DESC) ci
                LEFT JOIN Users u ON u.UserId = ca.UnlockedByUserId
                WHERE (@LockedOnly = 0 OR ca.LockedAtUtc IS NOT NULL)
                  AND (@Search IS NULL
                       OR CAST(ca.AccountNo AS NVARCHAR(20)) LIKE @Like OR ci.First_Name LIKE @Like
                       OR ca.Email LIKE @Like OR ca.Phone LIKE @Like)
                ORDER BY CASE WHEN ca.LockedAtUtc IS NULL THEN 1 ELSE 0 END, ca.LockedAtUtc DESC, ca.LastLoginUtc DESC",
                new { LockedOnly = lockedOnly, Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(), Like = $"%{search?.Trim()}%" })).ToList();

        // Lifts the lock and clears the wrong-code count; who and why are kept.
        public async Task<bool> UnlockAsync(long accountNo, int userId, string note) =>
            await _db.ExecuteAsync(@"
                UPDATE CustomerAccess
                SET LockedAtUtc = NULL, LockedReason = NULL, FailedCodes = 0, LastFailedUtc = NULL,
                    UnlockedAtUtc = SYSUTCDATETIME(), UnlockedByUserId = @UserId, UnlockNote = LEFT(@Note, 400)
                WHERE AccountNo = @AccountNo AND LockedAtUtc IS NOT NULL",
                new { AccountNo = accountNo, UserId = userId, Note = note }) == 1;

        // Messages sent to the phone since the customer last opened the
        // Inbox: how many, and the newest (for the pop-up).
        public async Task<(int Count, MessageLog? Newest)> GetNewMessagesAsync(long accountNo, string? imei, DateTime sinceUtc)
        {
            var args = new { Account = accountNo.ToString(), Imei = string.IsNullOrWhiteSpace(imei) ? accountNo.ToString() : imei, Since = sinceUtc };
            const string where = "AccountNo IN (@Account, @Imei) AND MessageType <> 'LoginCode' AND DateSent > @Since";
            var count = await _db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM MessageLogs WHERE {where}", args);
            var newest = count == 0 ? null : await _db.QueryFirstOrDefaultAsync<MessageLog>(
                $"SELECT TOP 1 Id, AccountNo, MessageType, Message, DateSent, MessageStatus FROM MessageLogs WHERE {where} ORDER BY DateSent DESC", args);
            return (count, newest);
        }

        public async Task MarkInboxSeenAsync(long accountNo) =>
            await _db.ExecuteAsync("UPDATE CustomerAccess SET InboxSeenUtc = SYSUTCDATETIME() WHERE AccountNo = @AccountNo", new { AccountNo = accountNo });

        public async Task SetEmailedUpToAsync(long accountNo, DateTime upToUtc) =>
            await _db.ExecuteAsync(@"
                UPDATE CustomerAccess SET EmailedUpToUtc = @UpTo
                WHERE AccountNo = @AccountNo AND (EmailedUpToUtc IS NULL OR EmailedUpToUtc < @UpTo)",
                new { AccountNo = accountNo, UpTo = upToUtc });

        public async Task<int?> GetLatestEndedContractIdAsync(long accountNo) =>
            await _db.QueryFirstOrDefaultAsync<int?>(@"
                SELECT TOP 1 ContractID FROM Contract_Info
                WHERE ID = @AccountNo AND EndDate IS NOT NULL
                ORDER BY EndDate DESC, ContractID DESC", new { AccountNo = accountNo });

        // Every payment on the account, re-pointed ones included -- the same
        // rows the reports total (AccountPaymentsSql).
        public async Task<List<Ranalo.Services.MyPayment>> GetPaymentsAsync(long accountNo) =>
            (await _db.QueryAsync<Ranalo.Services.MyPayment>(@"
                SELECT p.MpesaCode, p.AmountValue AS Amount, p.PaymentDateValue AS PaidAt
                FROM " + AccountPaymentsSql.For("@AccountNo") + @" p
                WHERE p.PaymentDateValue IS NOT NULL", new { AccountNo = accountNo })).ToList();
    }
}
