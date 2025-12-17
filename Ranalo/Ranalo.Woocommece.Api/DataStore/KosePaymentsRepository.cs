using Dapper;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Woocommece.Api.Models;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;

namespace Ranalo.Woocommece.Api.DataStore
{
    public class KosePaymentsRepository : IKosePaymentsRepository
    {
        private readonly IDbConnection _db;

        public KosePaymentsRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<int> InsertAsync(MpesaRecord record)
        {
            var sql = @"
            INSERT INTO [dbo].[KosePayments] (
               [AccountNo]
           ,[MpesaCode]
           ,[Amount]
           ,[PaymentDate]
           ,[AmountValue]
           ,[PaymentDateValue]
           ,[Created]
           ,[FirstName]
            )
            VALUES (
                @AccountNo, @MpesaCode, @Amount, @PaymentDate, @AmountValue, @PaymentDateValue, GETDATE(), @FirstName
            );

            SELECT CAST(SCOPE_IDENTITY() as bigint);
        ";

            return await _db.ExecuteScalarAsync<int>(sql, record);
        }

        public async Task<MpesaRecord?> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM [dbo].[KosePayments] WHERE Id = @Id";
            return await _db.QueryFirstOrDefaultAsync<MpesaRecord>(sql, new { Id = id });
        }

        public async Task<IEnumerable<MpesaRecord>> GetAllAsync()
        {
            var sql = "SELECT * FROM [dbo].[KosePayments] ORDER BY [PaymentDateValue] desc";
            return await _db.QueryAsync<MpesaRecord>(sql);
        }

        public async Task<List<string>> SaveToDatabaseAsync(Dictionary<string, List<MpesaRecord>> groupedRecords)
        {
            var response = new List<string>();

            const string insertQuery = @"
        INSERT INTO [dbo].[KosePayments] ([AccountNo], [MpesaCode], [Amount], [PaymentDate], [AmountValue], [PaymentDateValue], [Created])
        VALUES (@AccountNo, @MpesaCode, @Amount, @PaymentDate, @AmountValue, @PaymentDateValue, GETDATE())";

            foreach (var kvp in groupedRecords)
            {
                string groupKey = kvp.Key;
                List<MpesaRecord> records = kvp.Value;

                foreach (var record in records)
                {
                    //Check if already exist
                    var existingSql = @"SELECT * 
                                          FROM [dbo].[KosePayments]
                                        WHERE [AccountNo] = @AccountNo
                                          AND [MpesaCode] = @MpesaCode";
                    var existing = await _db.QueryFirstOrDefaultAsync<MpesaRecord>(existingSql, new { AccountNo = groupKey , MpesaCode = record.MpesaCode });

                    if(existing == null)
                    {
                        response.Add(groupKey);
                        await _db.ExecuteAsync(insertQuery, new
                        {
                            AccountNo = groupKey,
                            record.MpesaCode,
                            record.Amount,
                            record.PaymentDate,
                            record.AmountValue,
                            record.PaymentDateValue
                        });
                    }
                }
            }

            return response;
        }

        public async Task SaveDevicesToDatabaseAsync(List<Device> groupedRecords)
        {
            const string insertQuery = @"
        INSERT INTO [dbo].[Devices]
           ([Id]
           ,[Name]
           ,[ImeiNo]
           ,[ImeiNo2]
           ,[SerialNo]
           ,[IsTv]
           ,[PhoneNumber]
           ,[Model]
           ,[Make]
           ,[OsVersion]
           ,[SdkVersion]
           ,[Status]
           ,[Locked]
           ,[LockType]
           ,[DeviceGroupId]
           ,[AdminLockType]
           ,[AdminLocked]
           ,[AppVersionCode]
           ,[AppVersionName]
           ,[CreatedAt]
           ,[CustomerName]
           ,[CustomerEmail]
           ,[CustomerAddress]
           ,[CustomerPhoneNumber]
           ,[UnlockCode]
           ,[ValidityOfUnlockCode]
           ,[IsActivated]
           ,[IsLockedOnSimSwap]
           ,[FirstLockDate]
           ,[FirstLockDateIsoFormat]
           ,[NextLockDate]
           ,[NextLockDateIsoFormat]
           ,[EulaStatus]
           ,[EulaActionPerformedOn]
           ,[LastConnectedAt]
           ,[GettingStartedButtonClicked]
           ,[EnrollmentStatus]
           ,[EnrollmentFailureReason]
           ,[AdditionalSetupDone]
           ,[BatteryOptimizationGranted]
           ,[EnrolledOn]
           ,[DlcStatus])
        VALUES (@Id, 
                @Name, 
                @ImeiNo, 
                @ImeiNo2, 
                @SerialNo, 
                @IsTv,
                @PhoneNumber, 
                @Model,                 
                @Make, 
                @OsVersion, 
                @SdkVersion, 
                @Status, 
                @Locked, 
                @LockType, 
                @DeviceGroupId, 
                @AdminLockType, 
                @AdminLocked, 
                @AppVersionCode, 
                @AppVersionName, 
                @CreatedAt, 
                @CustomerName, 
                @CustomerEmail, 
                @CustomerAddress, 
                @CustomerPhoneNumber, 
                @UnlockCode, 
                @ValidityOfUnlockCode, 
                @IsActivated,                 
                @IsLockedOnSimSwap, 
                @FirstLockDate, 
                @FirstLockDateIsoFormat, 
                @NextLockDate, 
                @NextLockDateIsoFormat, 
                @EulaStatus, 
                @EulaActionPerformedOn, 
                @LastConnectedAt, 
                @GettingStartedButtonClicked, 
                @EnrollmentStatus, 
                @EnrollmentFailureReason, 
                @AdditionalSetupDone, 
                @BatteryOptimizationGranted, 
                @EnrolledOn, 
                @DlcStatus)";

                foreach (var record in groupedRecords)
                {
                    await _db.ExecuteAsync(insertQuery, record);
                }
        }

        public async Task UpdateDevicesToDatabaseAsync(List<Device> groupedRecords)
        {
            const string updateQuery = @"UPDATE [dbo].[Devices]
                                            SET [Name] = @Name
                                               ,[ImeiNo] = @ImeiNo
                                               ,[ImeiNo2] = @ImeiNo2
                                               ,[SerialNo] = @SerialNo
                                               ,[IsTv] = @IsTv
                                               ,[PhoneNumber] = @PhoneNumber
                                               ,[Model] = @Model
                                               ,[Make] = @Make
                                               ,[OsVersion] = @OsVersion
                                               ,[SdkVersion] = @SdkVersion
                                               ,[Status] = @Status
                                               ,[Locked] = @Locked
                                               ,[LockType] = @LockType
                                               ,[DeviceGroupId] = @DeviceGroupId
                                               ,[AdminLockType] = @AdminLockType
                                               ,[AdminLocked] = @AdminLocked
                                               ,[AppVersionCode] = @AppVersionCode
                                               ,[AppVersionName] = @AppVersionName
                                               ,[CreatedAt] = @CreatedAt
                                               ,[CustomerName] = @CustomerName
                                               ,[CustomerEmail] = @CustomerEmail
                                               ,[CustomerAddress] = @CustomerAddress
                                               ,[CustomerPhoneNumber] = @CustomerPhoneNumber
                                               ,[UnlockCode] = @UnlockCode
                                               ,[ValidityOfUnlockCode] = @ValidityOfUnlockCode
                                               ,[IsActivated] = @IsActivated
                                               ,[IsLockedOnSimSwap] = @IsLockedOnSimSwap
                                               ,[FirstLockDate] = @FirstLockDate
                                               ,[FirstLockDateIsoFormat] = @FirstLockDateIsoFormat
                                               ,[NextLockDate] = @NextLockDate
                                               ,[NextLockDateIsoFormat] = @NextLockDateIsoFormat
                                               ,[EulaStatus] = @EulaStatus
                                               ,[EulaActionPerformedOn] = @EulaActionPerformedOn
                                               ,[LastConnectedAt] = @LastConnectedAt
                                               ,[GettingStartedButtonClicked] = @GettingStartedButtonClicked
                                               ,[EnrollmentStatus] = @EnrollmentStatus
                                               ,[EnrollmentFailureReason] = @EnrollmentFailureReason
                                               ,[AdditionalSetupDone] = @AdditionalSetupDone
                                               ,[BatteryOptimizationGranted] = @BatteryOptimizationGranted
                                               ,[EnrolledOn] = @EnrolledOn
                                               ,[DlcStatus] = @DlcStatus
                                          WHERE [Id] = @Id";

            foreach (var record in groupedRecords)
            {
                await _db.ExecuteAsync(updateQuery, record);
            }
        }

        public async Task<int> AddContractAsync(ContractInfo contract)
        {
            var existingSql = @"SELECT ID FROM Contract_Info 
                                WHERE ID = @Id
                                AND EndDate IS NULL";
            int? existingContractId = await _db.QueryFirstOrDefaultAsync<int?>(existingSql, new { Id = contract.ID });

            if (existingContractId.HasValue)
            {
                return existingContractId.Value;
            }

            var sql = @"
            INSERT INTO Contract_Info
            (ID, Deposit, Daily, Weekly, Monthly, 
             rePayment_Intervals, Term_in_Months, Total_Loan, Total_Cost, First_Name)
            VALUES
            (@ID, @Deposit, @Daily, @Weekly, @Monthly, 
             @RePaymentIntervals, @TermInMonths, @TotalLoan, @TotalCost, @FirstName);
            SELECT CAST(SCOPE_IDENTITY() as int);";

            return await _db.ExecuteScalarAsync<int>(sql, contract);
        }

        public async Task UpdateOrderContract(long orderId, int contractId)
        {
            var sql = @"UPDATE Woo_Orders SET [ContractId] = @ContractId WHERE [OrderID] = @OrderId";

            await _db.ExecuteAsync(sql, new { ContractId = contractId, OrderId = orderId });
        }
    }
}
