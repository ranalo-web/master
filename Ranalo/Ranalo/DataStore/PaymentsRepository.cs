using Dapper;
using Microsoft.Identity.Client;
using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;
using System.Data;
using System.Diagnostics.Contracts;

namespace Ranalo.DataStore
{
    public class PaymentsRepository : IPaymentsRepository
    {
        private readonly IDbConnection _db;

        public PaymentsRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<KosePaymentsViewModel> GetOrphanedPaymentsAsync(int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countSql = @"SELECT COUNT(*)
                            FROM KosePayments kp
                        LEFT JOIN Devices D ON D.Id = TRY_CAST(kp.AccountNo AS BIGINT)
                        WHERE D.Id is null";

            var sql = @" SELECT kp.MpesaCode, kp.AccountNo, kp.AmountValue, kp.PaymentDateValue 
                        FROM KosePayments kp
                        LEFT JOIN Devices D ON D.Id = TRY_CAST(kp.AccountNo AS BIGINT)
                        WHERE D.Id is null
                        --AND D.[Status] = 'enrolled'
                        ORDER BY kp.PaymentDateValue desc
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var payments = await _db.QueryAsync<KosePayments>(sql, new { offset, pageSize });
            var totalRecords = await _db.QuerySingleAsync<int>(countSql);

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
            };


        }

        public async Task<KosePaymentsViewModel> GetAllPaymentsAsync(int page = 1, int pageSize = 10)
        {
            var offset = (page - 1) * pageSize;

            var countsql = @" SELECT COUNT(*) 
                        FROM KosePayments kp
                        WHERE [MessageId] IS NULL ";

            var totalRecords = await _db.QuerySingleAsync<int>(countsql);

            var sql = @" SELECT MpesaCode, AccountNo, AmountValue, PaymentDateValue 
                        FROM KosePayments kp
                        WHERE [MessageId] IS NULL 
                        ORDER BY PaymentDateValue desc
                        OFFSET @Offset ROWS 
                        FETCH NEXT @pageSize ROWS ONLY";

            var payments = await _db.QueryAsync<KosePayments>(sql, new { offset, pageSize });

            return new KosePaymentsViewModel()
            {
                CurrentPage = page,
                Payments = payments.ToList(),
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                TotalRecords = totalRecords
            };
        }

        public async Task<Dictionary<string, string>> GetAccountNames(IEnumerable<string> accountIds)
        {
            var validIds = accountIds
           .Where(id => int.TryParse(id, out _))
           .Select(int.Parse)
           .Distinct()
           .ToList();

            if (!validIds.Any())
                return new Dictionary<string, string>();

            const string query = @"SELECT ID, First_Name 
                               FROM Contract_Info 
                               WHERE ID IN @Ids";

            var results = _db.Query<(int ID, string FirstName)>(query, new { Ids = validIds });

            // Convert back to original string account numbers for consistency
            return results.ToDictionary(
                x => x.ID.ToString(),
                x => x.FirstName ?? string.Empty
            );
        }

        public async Task CreateMessageLogAsync(MessageLog record)
        {
            try
            {
                var sql = @"DECLARE @NewId TABLE (Id UNIQUEIDENTIFIER);

                            INSERT INTO MessageLogs
                            (
                                [Id],
                                [AccountNo],
                                [MessageType],
                                [Message],
                                [DateSent],
                                [MessageStatus],
                                [MessageError],
                                [PhoneNumber]
                            )
                            OUTPUT INSERTED.Id INTO @NewId
                            VALUES
                            (
                                @Id,
                                @AccountNo,
                                @MessageType,
                                @Message,
                                @DateSent,
                                @MessageStatus,
                                @MessageError,
                                @PhoneNumber
                            );

                        SELECT Id AS NewRecordId FROM @NewId;";

                var messageId = await _db.ExecuteScalarAsync<Guid>(sql, record);

                if (messageId != Guid.Empty)
                {
                    var updatePaymentSql = @"UPDATE [dbo].[KosePayments]
                                            SET [MessageId] = @Id
                                          WHERE [AccountNo] = @AccountNo
                                          AND [MpesaCode] = @MpesaCode"
                    ;

                    await _db.ExecuteAsync(updatePaymentSql, new { AccountNo = record.AccountNo, MpesaCode = record.PhoneNumber, Id = messageId });
                }
            }
            catch (Exception ex)
            {

                throw;
            }
            
        }
    }
}
