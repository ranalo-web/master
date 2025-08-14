using Dapper;
using Ranalo.Woocommece.Api.Models;
using System.Data;

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
            )
            VALUES (
                @AccountNo, @MpesaCode, @Amount, @PaymentDate, @AmountValue, @PaymentDateValue, GETDATE()
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

        public async Task SaveToDatabaseAsync(Dictionary<string, List<MpesaRecord>> groupedRecords)
        {
            const string insertQuery = @"
        INSERT INTO [dbo].[KosePayments] ([AccountNo], [MpesaCode], [Amount], [PaymentDate], [AmountValue], [PaymentDateValue], [Created])
        VALUES (@AccountNo, @MpesaCode, @Amount, @PaymentDate, @AmountValue, @PaymentDateValue, GETDATE())";

            foreach (var kvp in groupedRecords)
            {
                string groupKey = kvp.Key;
                List<MpesaRecord> records = kvp.Value;

                foreach (var record in records)
                {
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
    }
}
