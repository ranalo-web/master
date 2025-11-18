using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IDeviceService
    {
        Task<DevicesWithDealerViewModel> GetDevicesWithNoOrders(long dealerReference = 0, int page = 1, int pageSize = 10, string searchTerm = "");
        Task<DevicesWithDealerViewModel> GetDevicesWithNoContracts(long dealerReference = 0, int page = 1, int pageSize = 10, string searchTerm = "");

        Task<(string AccountNo, long? DeviceId)> GetCheckOrderIdLinkedAsync(long orderId);

        Task<bool> MpesaCodeIsValidAsync(string mpesaCode);

        Task<bool> OrderNumberIsValidAsync(long orderId);
        Task AssignMpesaToOrderAsync(int orderNumber, string newMpesa);
        Task<bool> MpesaCodeIsLinkedAsync(string newMpesa);
        Task<DevicesWithDealerViewModel> GetAllDevicesAsync(int? dealerId, string searchTerm, int page, int pageSize);

        Task<DevicesWithDealerViewModel> GetAllDevicesWithNoPaymentsAsync(int? dealerId, string searchTerm, int page, int pageSize);
    }
}