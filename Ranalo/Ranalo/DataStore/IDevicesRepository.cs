using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IDevicesRepository
    {
        Task<DevicesWithDealerViewModel> GetDevicesWithNoOrders(long dealerReference = 0, int page = 1, int pageSize = 10, string searchTerm = "", int? agentUserId = null);
        Task<DevicesWithDealerViewModel> GetDevicesWithNoContracts(long dealerReference = 0, int page = 1, int pageSize = 10, string searchTerm = "", int? agentUserId = null);
        Task<long> GetMetaDataByKeyForOrderNumber(int orderNumber, string metadataKey);
        Task<(string AccountNo, long? DeviceId)> GetOrderLinksAsync(long orderId);
        Task<bool> MpesaCodeIsAlreadyLinked(string newMpesa);
        Task<bool> MpesaCodeIsValidAsync(string mpesaCode);

        Task<Device?> GetDeviceByImei(string imei);
        Task UpdateDevicesToDatabaseAsync(List<Device> groupedRecords);

        Task<bool> OrderNumberIsValidAsync(long orderId);
        Task<int> UpdateMpesaForOrder(long orderId, string newMpesa);
        Task<Device?> GetDeviceByAccountId(long accountId);
        Task<DevicesWithDealerViewModel> GetAllDevicesAsync(int? dealerId, int page, int pageSize, string searchTerm);

        Task<DevicesWithDealerViewModel> GetDevicesWithNoPayments(int? dealerId, int page, int pageSize, string searchTerm, int? agentUserId = null);
        Task<DevicesWithDealerViewModel> GetAllDevicesByUserAccountIdAsync(int userId, int page, int pageSize, string searchTerm);

        // Devices.LockGroup (1 Nuovo, 2 Knox, 3 Transsion/PayTrigger) by
        // account. Accounts with no device row are left out.
        Task<Dictionary<long, int?>> GetLockGroupsAsync(IEnumerable<long> accountIds);
    }
}