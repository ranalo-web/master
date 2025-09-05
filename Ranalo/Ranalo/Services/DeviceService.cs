using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDevicesRepository _devicesRepository;

        public DeviceService(IDevicesRepository devicesRepository)
        {
            _devicesRepository = devicesRepository;
        }

        public async Task<DevicesWithDealerViewModel> GetDevicesWithNoOrders(long dealerReference = 0, int page = 1, int pageSize = 10, string searchTerm = "")
        {
            return await _devicesRepository.GetDevicesWithNoOrders(dealerReference, page, pageSize, searchTerm);
        }

        public async Task<(string AccountNo, long? DeviceId)> GetCheckOrderIdLinkedAsync(long orderId)
        {
            return await _devicesRepository.GetOrderLinksAsync(orderId);
        }

        public async Task<bool> MpesaCodeIsValidAsync(string mpesaCode)
        {
            return await _devicesRepository.MpesaCodeIsValidAsync(mpesaCode);
        }

        public async Task<bool> OrderNumberIsValidAsync(long orderId)
        {
            return await _devicesRepository.OrderNumberIsValidAsync(orderId);
        }

        public async Task AssignMpesaToOrderAsync(int orderNumber, string newMpesa)
        {
            //Get the metadata id from order number
            long metadataId = await _devicesRepository.GetMetaDataByKeyForOrderNumber(orderNumber, "mpesa_deposit_reference");
            //Update at Woo Commerce first then locally

            await SendMpesaUpdate(orderNumber, newMpesa);
        }

        public async Task<int> SendMpesaUpdate(long orderId, string newMpesa)
        {
            var client = new WooCommerceClient(
                "https://ranalocredit.com/wp-json/wc/v3",
                "ck_9bf5ade6a031f04b53bd31938d462895db40e00c",
                "cs_b2d5d61f3eae5093d85b7319905eb5942c614f99"
            );

            string result = await client.UpdateOrderMpesaAsync(orderId, newMpesa);

            return await _devicesRepository.UpdateMpesaForOrder(orderId, newMpesa);
        }
    }
}
