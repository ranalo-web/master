using System.Data;
using Dapper;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    // Database/Payments/003.
    public interface IStkRequestRepository
    {
        Task AddAsync(StkRequest request);
        Task<StkRequest?> GetAsync(string checkoutRequestId);

        // Records the result once; false if it was already recorded
        // (Safaricom can post the same result more than once).
        Task<bool> CompleteAsync(string checkoutRequestId, string status, int resultCode, string? resultDesc, string? receipt);
    }

    public class StkRequestRepository : IStkRequestRepository
    {
        private readonly IDbConnection _db;

        public StkRequestRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task AddAsync(StkRequest request) =>
            await _db.ExecuteAsync(@"
                INSERT INTO StkRequests (CheckoutRequestId, MerchantRequestId, AccountNo, Phone, Amount, Status, CreatedAtUtc)
                VALUES (@CheckoutRequestId, @MerchantRequestId, @AccountNo, @Phone, @Amount, @Status, @CreatedAtUtc)", request);

        public async Task<StkRequest?> GetAsync(string checkoutRequestId) =>
            await _db.QueryFirstOrDefaultAsync<StkRequest>(
                "SELECT * FROM StkRequests WHERE CheckoutRequestId = @Id", new { Id = checkoutRequestId });

        public async Task<bool> CompleteAsync(string checkoutRequestId, string status, int resultCode, string? resultDesc, string? receipt) =>
            await _db.ExecuteAsync(@"
                UPDATE StkRequests
                SET Status = @Status, ResultCode = @ResultCode, ResultDesc = LEFT(@ResultDesc, 400),
                    MpesaReceipt = @Receipt, CompletedAtUtc = SYSUTCDATETIME()
                WHERE CheckoutRequestId = @Id AND Status = 'Sent'",
                new { Id = checkoutRequestId, Status = status, ResultCode = resultCode, ResultDesc = resultDesc, Receipt = receipt }) == 1;
    }
}
