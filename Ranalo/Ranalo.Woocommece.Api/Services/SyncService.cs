using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Models;

namespace Ranalo.Woocommece.Api.Services
{
    public class SyncService : ISyncService
    {
        private readonly ISyncLogsRepository _syncLogsRepository;
        private readonly IWooOrderRepository _wooOrderRepository;
        private readonly IWooOrderProductRepository _wooOrderProductRepository;
        private readonly IKosePaymentsRepository _kosePaymentsRepository;
        public SyncService(ISyncLogsRepository syncLogsRepository, 
            IWooOrderProductRepository wooOrderProductRepository, 
            IWooOrderRepository wooOrderRepository,
            IKosePaymentsRepository kosePaymentsRepository)
        {
            _syncLogsRepository = syncLogsRepository;
            _wooOrderProductRepository = wooOrderProductRepository;
            _wooOrderRepository = wooOrderRepository;
            _kosePaymentsRepository = kosePaymentsRepository;
        }
        public async Task<DataSyncLog?> GetLastSycnLogDetails()
        {

            return await _syncLogsRepository.GetLastSyncLogAsync();
        }

        public async Task<WooOrder?> GetLastCreatedOrderAsync()
        {
            return await _wooOrderRepository.GetLastSyncedOrderAsync();
        }

        public async Task<int> CreateOrderAsync(List<WooOrder> orders)
        {
            try
            {
                foreach (var order in orders)
                {
                    //No Mpesa no honey
                    if(string.IsNullOrEmpty(order.MpesaDepositRef))
                    {
                        continue;
                    }
                    //Check if recor exists
                    var existingOrder = await _wooOrderRepository.GetByOrderIdAsync(order.OrderID);
                    if (existingOrder != null)
                    {
                        await _wooOrderRepository.UpdateAsync(order);
                        continue;
                    }

                    var orderId = await _wooOrderRepository.InsertAsync(order);

                    
                    foreach (var product in order.Products)
                    {
                        product.OrderId = orderId;

                        await _wooOrderProductRepository.InsertAsync(product);
                    }

                    foreach (var imageDetail in order.ImagesMetadata)
                    {
                        await _wooOrderProductRepository.InsertImageDetailsAsync(orderId, imageDetail);
                    }



                }
            }
            catch (Exception ex)
            {

                return 0;
            }

            return 1;
        }

        public async Task<List<int>> UpdateImagesAsync(long orderId, List<ImagesMetadata> imagesForUpdate)
        {
            var resultIds = new List<int>();
            foreach (var image in imagesForUpdate)
            {
                var insertedId = await _wooOrderProductRepository.InsertImageDetailsAsync(orderId, image);

                resultIds.Add(insertedId);
            }

            return resultIds;
        }

        public async Task LogLastSyncDetails(DataSyncLog log)
        {
            await _syncLogsRepository.InsertAsync(log);
        }

        public async Task<SyncPaymentsLog?> GetLastTransactionDateAsync()
        {
            return await _syncLogsRepository.GetLastPaymentLog();
        }

        public async Task LogLastPaymentSyncDetails(SyncPaymentsLog log)
        {
            await _syncLogsRepository.InsertPaymentLogAsync(log);
        }

        public async Task<int> CreateKosePaymentAsync(MpesaRecord record)
        {
            return await _kosePaymentsRepository.InsertAsync(record);
        }

        public async Task CreateKoseBatchPaymentAsync(Dictionary<string, List<MpesaRecord>> records)
        {
            await _kosePaymentsRepository.SaveToDatabaseAsync(records);
        }

        public async Task CreateDevicesToDatabaseAsync(List<Device> groupedRecords)
        {
            await _kosePaymentsRepository.SaveDevicesToDatabaseAsync(groupedRecords);
        }

        public async Task<Device?> GetLatestDeviceId()
        {
            return await _syncLogsRepository.GetLatestDeviceAsync();
        }
    }
}
