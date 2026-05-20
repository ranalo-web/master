using Dapper;
using DocumentFormat.OpenXml.Drawing;
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
                        LEFT JOIN Devices D ON D.Id = kp.AccountNoBigint
                        WHERE D.Id is null";

            var sql = @" SELECT kp.MpesaCode, kp.AccountNo, kp.AmountValue, kp.PaymentDateValue 
                        FROM KosePayments kp
                        LEFT JOIN Devices D ON D.Id = kp.AccountNoBigint
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

        public async Task<List<PaymentMessage>?> GetAllPaymentsForMessagesAsync()
        {

            var sql = @"SELECT 
	                        kp.Id,
	                        MpesaCode, 
	                        AccountNoBigint as AccountNo, 
	                        AmountValue, 
	                        PaymentDateValue,
	                        d.ImeiNo as Imei,
	                        d.LockGroup,
	                        ISNULL(kp.FirstName, 'Customer') as FirstName
                        FROM KosePayments kp
                        LEFT JOIN Devices d on d.Id = kp.AccountNoBigint
                        WHERE [MessageId] IS NULL 
                        ORDER BY PaymentDateValue asc";

            var payments = await _db.QueryAsync<PaymentMessage>(sql);

            return payments.ToList();
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
                        ORDER BY PaymentDateValue asc
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
                               WHERE ID IN @Ids
                                AND EndDate IS NULL";

            var results = _db.Query<(int ID, string FirstName)>(query, new { Ids = validIds });

            // Convert back to original string account numbers for consistency
            var dict = results
                .GroupBy(x => x.ID)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => g.First().FirstName ?? string.Empty
                );
            return dict;
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

                if (messageId != Guid.Empty && !string.IsNullOrEmpty(record.PhoneNumber))
                {
                    var updatePaymentSql = @"UPDATE [dbo].[KosePayments]
                                            SET [MessageId] = @Id
                                          WHERE [MpesaCode] = @MpesaCode"
                    ;

                    await _db.ExecuteAsync(updatePaymentSql, new { MpesaCode = record.PhoneNumber, Id = messageId });
                }
            }
            catch (Exception ex)
            {

                throw;
            }
            
        }

        public async Task<List<AccountSummary>> GetLiveQualifyingLockAccounts()
        {
            var sql = @"WITH payment_agg AS (
                        SELECT
                            AccountNoBigint AS account_no,
                            SUM(AmountValue) AS Total_Paid,
                            MIN(PaymentDateValue) AS First_Payment_Date,
                            MAX(PaymentDateValue) AS Last_Payment_Date
                        FROM KosePayments
                        GROUP BY AccountNoBigint
                    ),
                    -- Step 2: First payment details
                    first_payment_details AS (
                        SELECT
                            p.AccountNoBigint AS account_no,
                            p.AmountValue AS First_Paid_Amount,
                            p.PaymentDateValue AS FirstPaidDate,
                            p.MpesaCode AS First_MPesaCode
                        FROM KosePayments p
                        INNER JOIN payment_agg a
                            ON p.AccountNoBigint = a.account_no
                            AND p.PaymentDateValue = a.First_Payment_Date
                    ),
                    -- Step 3: Last payment details
                    last_payment_details AS (
                        SELECT
                            p.AccountNoBigint AS account_no,
                            p.AmountValue AS Last_Paid_Amount,
                            p.PaymentDateValue AS LastPaidDate,
                            p.MpesaCode AS Last_MPesaCode
                        FROM KosePayments p
                        INNER JOIN payment_agg a
                            ON p.AccountNoBigint = a.account_no
                            AND p.PaymentDateValue = a.Last_Payment_Date
                    ),
                    final_report AS (
                    SELECT
                        d.id,
                    	c.First_Name,
                        d.model,
                        d.make,
                        d.locked,
                        d.FirstLockDateIsoFormat AS First_Lock_Date,
                        d.NextLockDateIsoFormat AS Next_Lock_Date,
                        d.LastConnectedAt AS last_connected_at,
                        c.Deposit,
                        c.Daily,
                        c.Weekly,
                        c.Monthly,
                        c.Term_in_Months,
                        dp.Total_Paid,
                        dp.First_Payment_Date,
                        dp.Last_Payment_Date,
                        f.First_Paid_Amount,
                        l.Last_Paid_Amount,
                        f.First_MPesaCode,
                        l.Last_MPesaCode,
                        c.rePayment_Intervals,
                        -- Contract end date
                        CASE 
                            WHEN dp.First_Payment_Date IS NULL THEN NULL
                            ELSE DATEADD(DAY, c.Term_in_Months * 30, dp.First_Payment_Date)
                        END AS Contract_End_Date,
                        -- Days since first payment
                        DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) AS No_Days_Lifetime,
                        DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) AS No_Days_Units,
                        -- Loan balance
                        (c.Deposit + (c.Daily * 30 * c.Term_in_Months)
                            + (c.Weekly * (30.0/7) * c.Term_in_Months)
                            + (c.Monthly * c.Term_in_Months) - dp.Total_Paid) AS Loan_Balance,
                        -- Daily payment total
                        ((c.Daily) + (c.Weekly/7.0) + (c.Monthly/30.0)) AS DailyPaymentALL,
                        -- Arrears calculation
                        (dp.Total_Paid - (c.Deposit + (c.Daily * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()))
                            + (c.Weekly * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) / 7.0)
                            + (c.Monthly * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) / 30.0))) AS Arrears,
                        -- Lock status
                        CASE 
                            WHEN (dp.Total_Paid - (c.Deposit + (c.Daily * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()))
                                + (c.Weekly * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) / 7.0)
                                + (c.Monthly * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) / 30.0))) > 0
                            THEN 'unlocked' 
                            ELSE 'complete' 
                        END AS Lock_Status_Pmt,
                        -- Units left
                        CASE 
                            WHEN ((c.Daily + (c.Weekly/7.0) + (c.Monthly/30.0)) = 0) THEN 0
                            ELSE (dp.Total_Paid - (c.Deposit + (c.Daily * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()))
                                + (c.Weekly * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) / 7.0)
                                + (c.Monthly * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) / 30.0)))
                                 / (c.Daily + (c.Weekly/7.0) + (c.Monthly/30.0))
                        END AS Units_Left,
                        -- Auto lock date
                        DATEADD(
                            DAY,
                            CASE 
                                WHEN (c.Daily + (c.Weekly/7.0) + (c.Monthly/30.0)) = 0 THEN 0
                                ELSE (dp.Total_Paid - (c.Deposit + (c.Daily * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()))
                                    + (c.Weekly * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) / 7.0)
                                    + (c.Monthly * DATEDIFF(DAY, dp.First_Payment_Date, GETDATE()) / 30.0)))
                                    / (c.Daily + (c.Weekly/7.0) + (c.Monthly/30.0))
                            END,
                            GETDATE()
                        ) AS Auto_Lock_Date_Pmt
                    FROM Devices d
                    LEFT JOIN Contract_Info c ON d.id = c.ID AND c.EndDate IS NULL
                    LEFT JOIN payment_agg dp ON d.id = dp.account_no
                    LEFT JOIN first_payment_details f ON dp.account_no = f.account_no
                    LEFT JOIN last_payment_details l ON dp.account_no = l.account_no
                    )
                    
                    SELECT id AS AccountId,
                    	   First_Name AS FirstName,
                    	   Auto_Lock_Date_Pmt AS AutoLockDatePmtR,
                    	   Units_Left AS UnitsLeft
                    FROM final_report
                    WHERE Arrears < 0 
                    AND Loan_Balance > 0";

            var candidates = await _db.QueryAsync<AccountSummary>(sql);

            return candidates.ToList();
        }
    }
}
