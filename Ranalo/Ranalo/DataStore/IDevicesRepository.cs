using Ranalo.Models;

namespace Ranalo.DataStore
{
    public interface IDevicesRepository
    {
        Task<DevicesWithDealerViewModel> GetDevicesWithNoOrders(long dealerReference = 0, int page = 1, int pageSize = 10, string searchTerm = "");
        Task<long> GetMetaDataByKeyForOrderNumber(int orderNumber, string metadataKey);
        Task<(string AccountNo, long? DeviceId)> GetOrderLinksAsync(long orderId);
        Task<bool> MpesaCodeIsAlreadyLinked(string newMpesa);
        Task<bool> MpesaCodeIsValidAsync(string mpesaCode);

        Task<bool> OrderNumberIsValidAsync(long orderId);
        Task<int> UpdateMpesaForOrder(long orderId, string newMpesa);
        Task<Device?> GetDeviceByAccountId(long accountId);

    }
}