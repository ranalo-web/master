using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ranalo.Calculator.Logic.Contract;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Models;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Ranalo.Woocommece.Api.Services
{
    public class SyncService : ISyncService
    {
        private readonly ISyncLogsRepository _syncLogsRepository;
        private readonly IWooOrderRepository _wooOrderRepository;
        private readonly IWooOrderProductRepository _wooOrderProductRepository;
        private readonly IKosePaymentsRepository _kosePaymentsRepository;
        private readonly IContractCalculatorService _calculatorService;
        public SyncService(ISyncLogsRepository syncLogsRepository, 
            IWooOrderProductRepository wooOrderProductRepository, 
            IWooOrderRepository wooOrderRepository,
            IKosePaymentsRepository kosePaymentsRepository,
            IContractCalculatorService calculatorService)
        {
            _syncLogsRepository = syncLogsRepository;
            _wooOrderProductRepository = wooOrderProductRepository;
            _wooOrderRepository = wooOrderRepository;
            _kosePaymentsRepository = kosePaymentsRepository;
            _calculatorService = calculatorService;
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
                    //if(string.IsNullOrEmpty(order.MpesaDepositRef))
                    //{
                    //    continue;
                    //}
                    //Check if recor exists
                    var existingOrder = await _wooOrderRepository.GetByOrderIdAsync(order.OrderID);
                    if (existingOrder != null)
                    {
                        await _wooOrderRepository.UpdateAsync(order);
                        continue;
                    }

                    var orderId = await _wooOrderRepository.InsertAsync(order);

                    //We need to create contract using the orders and hope the Mpesa provided is valid
                    if (!string.IsNullOrEmpty(order.MpesaDepositRef))
                    {
                        var account = await _wooOrderRepository.GetAccountDetailsByMpesa(order.MpesaDepositRef);
                        if(account != null)
                        {
                            await DoCreateContractInfo(order, account, 12);
                        }
                    }
                    
                    
                    foreach (var product in order.Products)
                    {
                        product.OrderId = orderId;

                        await _wooOrderProductRepository.InsertAsync(product);
                    }

                    foreach (var imageDetail in order.ImagesMetadata)
                    {
                        await _wooOrderProductRepository.InsertImageDetailsAsync(orderId, imageDetail);
                    }

                    if(order.NextOfKin != null)
                    {
                        await _wooOrderProductRepository.InsertNextOfKinAsync(order.NextOfKin);
                    }
                    if(order.MetaData != null)
                    {
                        await _wooOrderProductRepository.InsertMetaDataAsync(order.MetaData);
                    }
                }
            }
            catch (Exception ex)
            {

                return 0;
            }

            return 1;
        }

        private async Task DoCreateContractInfo(WooOrder order, MpesaRecord account, decimal termsInMonths)
        {
            var dailyRate = _calculatorService.CalculateDailyRate(order.TotalAmount);
            var deposit = _calculatorService.CalculateDeposit(order.TotalAmount);
            var contract = new ContractInfo()
            {
                ID = int.Parse(account.AccountNo),
                Deposit = deposit,
                Daily = dailyRate,
                Weekly = 0,//_calculatorService.CalculateWeekleyRate(dailyRate),
                Monthly = 0, //_calculatorService.CalculateMonthlyRate(dailyRate),
                RePaymentIntervals = "Daily",
                TotalCost = _calculatorService.CalculateTotalCost(dailyRate, deposit, termsInMonths),
                TotalLoan = _calculatorService.CalculateTotalLoan(dailyRate, termsInMonths)
            };

            await _kosePaymentsRepository.AddContractAsync(contract);
        }
        public async Task<List<string>> CreateContractsForEligibleOrders()
        {
            var accountsNos = new List<string>();
            //Get All eligible orders
            var eligible = await _wooOrderProductRepository.GetContractEligibleOrders();

            foreach (var order in eligible)
            {
                if (string.IsNullOrEmpty(order.FirstName))
                {
                    continue;
                }
                await CreateContractSingle(order);

                accountsNos.Add(order.AccountNo);
            }

            return accountsNos;
        }

        public async Task CreateContractSingle(ContractCreateDto order)
        {
            var dailyRate = _calculatorService.CalculateDailyRate(order.TotalAmount);
            var deposit = _calculatorService.CalculateDeposit(order.TotalAmount);
            var contract = new ContractInfo()
            {
                ID = int.Parse(order.AccountNo),
                Deposit = deposit,
                Daily = dailyRate,
                Weekly = 0,
                Monthly = 0,
                RePaymentIntervals = "Daily",
                TotalCost = _calculatorService.CalculateTotalCost(dailyRate, deposit, 12),
                TotalLoan = _calculatorService.CalculateTotalLoan(dailyRate, 12),
                FirstName = order.FirstName,
                TotalAmount = order.TotalAmount
            };

            var contractId = await _kosePaymentsRepository.AddContractAsync(contract);

            //Update the Orders with Contract Id 
            await _kosePaymentsRepository.UpdateOrderContract(order.OrderId, contractId);
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

        public async Task UpdateNextOfKeen(long orderId, Contact nextOfKin)
        {
            if (nextOfKin != null)
            {
                await _wooOrderProductRepository.InsertNextOfKinAsync(nextOfKin);
            }
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

        public async Task<List<string>> CreateKoseBatchPaymentAsync(Dictionary<string, List<MpesaRecord>> records)
        {
            return await _kosePaymentsRepository.SaveToDatabaseAsync(records);
        }

        public async Task CreateDevicesToDatabaseAsync(List<Device> groupedRecords)
        {
            await _kosePaymentsRepository.SaveDevicesToDatabaseAsync(groupedRecords);
        }

        public async Task UpdateDevicesToDatabaseAsync(List<Device> groupedRecords)
        {
            await _kosePaymentsRepository.UpdateDevicesToDatabaseAsync(groupedRecords);
        }

        public async Task<Device?> GetLatestDeviceId()
        {
            return await _syncLogsRepository.GetLatestDeviceAsync();
        }

        #region sync Services

        public async Task<List<WooOrder>> SyncWooOrders()
        {
            // Start from last sync date or fallback to 3 days ago
            var iso8601UtcDate = DateTime.UtcNow.AddDays(-11).Date.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var lastLog = await GetLastSycnLogDetails();
            if (lastLog != null)
            {
                var lastOrderDate = lastLog.LastOrderCreatedDate;
                iso8601UtcDate = lastOrderDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
            }

            DateTime lastSyncDate = DateTime.UtcNow;
            var mappedOrders = new List<WooOrder>();
            int page = 1;
            string recordsToSync;

            do
            {
                // Fetch paginated data
                recordsToSync = await SecuredApiGetRequestStringResponse(iso8601UtcDate, page);

                if (!string.IsNullOrWhiteSpace(recordsToSync) && recordsToSync.Trim() != "[]")
                {
                    var values = JArray.Parse(recordsToSync);
                    if (values.Count > 0)
                    {
                        var lastItem = values.Last;
                        lastSyncDate = lastItem["date_modified"]?.Value<DateTime?>() ?? lastSyncDate;

                        foreach (var value in values)
                        {
                            var mappedFromJson = MapOrderFromWoo(value);
                            mappedOrders.Add(mappedFromJson);
                        }
                    }
                }

                page++;

            } while (!string.IsNullOrWhiteSpace(recordsToSync) && recordsToSync.Trim() != "[]");

            // Save orders if any were fetched
            if (mappedOrders.Any())
            {
                var createdId = await CreateOrderAsync(mappedOrders);

                if (createdId == 1)
                {
                    var log = new DataSyncLog
                    {
                        LastOrderCreatedDate = lastSyncDate,
                        Status = SyncStatus.Success,
                        SyncDate = DateTime.UtcNow,
                        Type = SyncType.Orders
                    };

                    await LogLastSyncDetails(log);
                }

                //foreach (var mappedOrder in mappedOrders)
                //{
                //    var orderId = await UpdateImagesAsync(mappedOrder.OrderID, mappedOrder.ImagesMetadata);

                //    Debug.WriteLine(orderId);
                //}
            }

            return mappedOrders;
        }

        public async Task<List<string>> SyncPayments()
        {
            var allPayments = await SecuredApiGetRequestStringResponse();

            if (string.IsNullOrEmpty(allPayments))
            {
                return null;
            }

            List<MpesaRecord>? records = JsonConvert.DeserializeObject<List<MpesaRecord>>(allPayments);

            if (records == null)
            {
                return null;
            }

            //With these records we need check the last transaction date.
            DateTime? lastSyncDate = null;
            var lastTransactionDate = await GetLastTransactionDateAsync();

            if (lastTransactionDate != null)
            {
                lastSyncDate = lastTransactionDate.LastPaymentDate;
            }
            Dictionary<string, List<MpesaRecord>>? grouped = new Dictionary<string, List<MpesaRecord>>();

            if (lastSyncDate == null)
            {
                grouped = records
                .GroupBy(r => r.AccountNo)
                .ToDictionary(g => g.Key, g => g.ToList());
            }
            else
            {
                grouped = records.Where(x => x.PaymentDateValue > lastSyncDate)
                .GroupBy(r => r.AccountNo)
                .ToDictionary(g => g.Key, g => g.ToList());
            }

            //Write the lot to db
            var addedRecords = await CreateKoseBatchPaymentAsync(grouped);

            var lastRecord = records.LastOrDefault();

            var logRecord = new SyncPaymentsLog()
            {
                LastPaymentDate = lastRecord.PaymentDateValue,
                LastPaymentId = lastRecord.Id

            };
            await LogLastPaymentSyncDetails(logRecord);

            return addedRecords;
        }

        public async Task<List<Device>> DeviceUnlockPull()
        {
            int? currentPage = 1;
            var currentDevices = new List<Device>();
            while (currentPage != null)
            {
                var result = await SecuredApiGetDeviceLockingRequestStringResponse((int)currentPage);

                var device = JsonConvert.DeserializeObject<LockDevices>(result);

                if (device != null && device.Devices != null)
                {
                    currentDevices.AddRange(device.Devices);
                    currentPage = device.NextPage;
                }
                else
                {
                    currentPage = null;
                }
            }


            var devicesToCreate = new List<Device>();
            var latestDeviceId = await GetLatestDeviceId();

            if (latestDeviceId != null)
            {
                devicesToCreate = currentDevices.Where(x => x.Id > latestDeviceId.Id).ToList();
                if (devicesToCreate.Any())
                {
                    await CreateDevicesToDatabaseAsync(devicesToCreate);
                }
            }

            //Lets update them all
            await UpdateDevicesToDatabaseAsync(currentDevices);

            return devicesToCreate;
        }

        public async Task<WooOrder> OrderById(int orderId)
        {
            var order = await SecuredApiGetSingleOrderRequestStringResponse(orderId);
            var mappedOrder = new WooOrder();
            if (string.IsNullOrEmpty(order))
            {
                return mappedOrder;
            }

            if (!string.IsNullOrWhiteSpace(order) && order.Trim() != "[]")
            {
                var value = JToken.Parse(order);

                var mappedFromJson = MapOrderFromWoo(value);
                mappedOrder = mappedFromJson;
            }

            return mappedOrder;
        }

        public async Task<List<int>> SyncUpdateImagesWooOrders()
        {
            var updatedOrders = new List<int>();
            // Start from last sync date or fallback to 3 days ago
            var iso8601UtcDate = DateTime.UtcNow.AddDays(-7).Date.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var mappedOrders = new List<WooOrder>();
            int page = 1;
            string recordsToSync;

            do
            {
                // Fetch paginated data
                recordsToSync = await SecuredApiGetRequestStringResponse(iso8601UtcDate, page);

                if (!string.IsNullOrWhiteSpace(recordsToSync) && recordsToSync.Trim() != "[]")
                {
                    var values = JArray.Parse(recordsToSync);
                    if (values.Count > 0)
                    {
                        foreach (var value in values)
                        {
                            var mappedFromJson = MapOrderFromWoo(value);
                            mappedOrders.Add(mappedFromJson);
                        }
                    }
                }

                page++;

            } while (!string.IsNullOrWhiteSpace(recordsToSync) && recordsToSync.Trim() != "[]");

            if (mappedOrders.Any())
            {
                foreach (var mappedOrder in mappedOrders)
                {
                    var orderId = await UpdateImagesAsync(mappedOrder.OrderID, mappedOrder.ImagesMetadata);

                    updatedOrders.AddRange(orderId);
                }
            }

            return updatedOrders;
        }

        public async Task<List<int>> SyncUpdateNextOfKinWooOrders()
        {
            var updatedOrders = new List<int>();
            // Start from last sync date or fallback to 3 days ago
            var iso8601UtcDate = DateTime.UtcNow.AddDays(-7).Date.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var mappedOrders = new List<WooOrder>();
            int page = 1;
            string recordsToSync;

            do
            {
                // Fetch paginated data
                recordsToSync = await SecuredApiGetRequestStringResponse(iso8601UtcDate, page);

                if (!string.IsNullOrWhiteSpace(recordsToSync) && recordsToSync.Trim() != "[]")
                {
                    var values = JArray.Parse(recordsToSync);
                    if (values.Count > 0)
                    {
                        foreach (var value in values)
                        {
                            var mappedFromJson = MapOrderFromWoo(value);
                            mappedOrders.Add(mappedFromJson);
                        }
                    }
                }

                page++;

            } while (!string.IsNullOrWhiteSpace(recordsToSync) && recordsToSync.Trim() != "[]");

            if (mappedOrders.Any())
            {
                foreach (var mappedOrder in mappedOrders)
                {
                    await UpdateNextOfKeen(mappedOrder.OrderID, mappedOrder.NextOfKin);
                }
            }

            return updatedOrders;
        }
        #endregion

        private async Task<string> SecuredApiGetRequestStringResponse(string iso8601UtcDate, int page = 1)
        {
            var consumerKey = "ck_0090896477d37b5ce6e006eabd7f579aacb1a97f";
            var consumerSecret = "cs_2969d990e2967d37aab8078572ee30020417467f";
            var baseUrl = "https://ranalocredit.com/wp-json/wc/v3";
            var client = new HttpClient();
            var retries = 0;
            const int maxRetries = 5;

            while (retries < maxRetries)
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var authToken = Encoding.ASCII.GetBytes($"{consumerKey}:{consumerSecret}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                // var iso8601UtcDate = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-ddTHH:mm:ssZ"); ;

                var queryParams = new Dictionary<string, string>
            {
                { "per_page", "10" },
                { "page", page.ToString() },
                { "consumer_key", consumerKey },
                { "consumer_secret", consumerSecret },
                { "modified_after", iso8601UtcDate },
                { "orderby", "modified" },
                { "order", "asc" }
            };

                var queryString = await new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
                var urlWithParams = $"{baseUrl}/orders?{queryString}";


                var response = await client.GetAsync(urlWithParams);

                if (response.IsSuccessStatusCode)
                {
                    
                    var content = await response.Content.ReadAsStringAsync();
                    if (content.Trim() == "[]")
                    {
                        // Empty result set
                        return "";
                    }
                    return content;
                }

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? Math.Pow(2, retries);
                    await Task.Delay(TimeSpan.FromSeconds(retryAfter));
                    retries++;
                    continue;
                }
                response.EnsureSuccessStatusCode();
            }

            return "";

        }

        private async Task<string> SecuredApiGetSingleOrderRequestStringResponse(int orderId)
        {
            var consumerKey = "ck_9bf5ade6a031f04b53bd31938d462895db40e00c";
            var consumerSecret = "cs_b2d5d61f3eae5093d85b7319905eb5942c614f99";
            var baseUrl = "https://ranalocredit.com/wp-json/wc/v3";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var authToken = Encoding.ASCII.GetBytes($"{consumerKey}:{consumerSecret}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

            // var iso8601UtcDate = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-ddTHH:mm:ssZ"); ;

            var urlWithParams = $"{baseUrl}/orders/{orderId}";


            var response = await client.GetAsync(urlWithParams);

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            if (content.Trim() == "[]")
            {
                // Empty result set
                return "";
            }
            return content;
        }

        private async Task<string> SecuredApiGetDeviceLockingRequestStringResponse(int page = 1)
        {
            var consumerKey = "Token 8efccf09d4874f88ba2a62f5db8d8efc";
            var baseUrl = "https://app.nuovopay.com/dm/api/v1/devices.json";
            var client = new HttpClient();
            var authToken = Encoding.ASCII.GetBytes(consumerKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("Authorization", consumerKey);
            // var iso8601UtcDate = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-ddTHH:mm:ssZ"); ;

            var queryParams = new Dictionary<string, string>
            {
                { "page", page.ToString() }
            };

            var queryString = await new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
            var urlWithParams = $"{baseUrl}?{queryString}";


            var response = await client.GetAsync(urlWithParams);

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            if (content.Trim() == "[]")
            {
                // Empty result set
                return "";
            }
            return content;
        }

        private async Task<string> SecuredApiGetRequestStringResponse()
        {
            var baseUrl = "https://kosewefarms.com/malipo/Payment_Verification_API.php";
            var retries = 0;
            const int maxRetries = 5;
            var client = new HttpClient();
            var error = "";
            while (retries < maxRetries)
            {
                var response = await client.GetAsync(baseUrl);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? Math.Pow(2, retries);
                    await Task.Delay(TimeSpan.FromSeconds(retryAfter));
                    retries++;
                    continue;
                }

                response.EnsureSuccessStatusCode(); // throws for non-2xx
            }
            return "";
        }

        public static string GetMpesaGroup(List<MpesaRecord> records, string mpesaCode)
        {
            var match = records.FirstOrDefault(r => r.MpesaCode == mpesaCode);
            return match != null ? $"Group-{match.AccountNo}" : "Unknown Group";
        }
        private WooOrder MapOrderFromWoo(JToken value)
        {
            return new WooOrder
            {
                //Id = value.Value<int>(),
                OrderID = value["id"]?.Value<int?>() ?? 0,
                Status = value["status"]?.Value<string?>() ?? "",
                DateCreated = value["date_created"]?.Value<DateTime?>() ?? null,
                DateModified = value["date_modified"]?.Value<DateTime?>() ?? null,
                TotalAmount = value["total"]?.Value<decimal?>() ?? 0,
                CustomerId = value["customer_id"]?.Value<long?>() ?? 0,
                FirstName = value["billing"]?["first_name"]?.Value<string?>() ?? "",
                LastName = value["billing"]?["last_name"]?.Value<string?>() ?? "",
                Address1 = value["billing"]?["address_1"]?.Value<string?>() ?? "",
                Email = value["billing"]?["email"]?.Value<string?>() ?? "",
                Phone = value["billing"]?["phone"]?.Value<string?>() ?? "",
                IMEI = value["billing"]?["billing_imei"]?.Value<string?>() ?? "",
                NationalId = value["billing"]?["billing_identification"]?.Value<string?>() ?? "",
                DOB = value["billing"]?["billing_date_of_birth"]?.Value<string?>() ?? "",
                DealerRef = value["billing"]?["billing_referral_code"]?.Value<string?>() ?? "",
                CustPhone = value["identity_verification"]?["owners_phone"]?.Value<string?>() ?? "",
                CustEmail = value["billing"]?["billing_email_of_your_next_of_kin"]?.Value<string?>() ?? "",
                MpesaDepositRef = value["identity_verification"]?["mpesa_deposit_reference"]?.Value<string?>() ?? value["meta_data"]?["mpesa_deposit_reference"]?.Value<string?>(),
                Products = MapOrderProducts(value),
                ImagesMetadata = ExtractDocumentMetadata(value["meta_data"].ToString()),
                NextOfKin = ExtractNextKin(value["id"]?.Value<int?>() ?? 0, value["meta_data"].ToString()),
                MetaData = ExtractAllMetadata(value)
            };
        }

        private UserMetaData ExtractAllMetadata(JToken value)
        {
            var orderId = value["id"]?.Value<long?>();

            var metaDataJson = value["meta_data"]?.ToString();
            var metaData = metaDataJson != null
                ? JsonConvert.DeserializeObject<List<MetaDataEntry>>(metaDataJson)
                : new List<MetaDataEntry>();

            return new UserMetaData
            {
                Id = Guid.NewGuid(),
                OrderId = orderId ?? 0,
                MetaData = metaData
            };
        }

        private Contact ExtractNextKin(long orderId, string jsonString)
        {
            var metaDataEntries = JsonConvert.DeserializeObject<List<MetaDataEntry>>(jsonString);

            var metaDataList = new List<MetaDataEntry>();
            
                try
                {
                    return MapFromMetaData(orderId, metaDataEntries);
                }
                catch (JsonReaderException)
                {
                    // Handle bad JSON inside value field (e.g. a non-object value)
                    //continue;
                }

            return null;
        }

        public static Contact MapFromMetaData(long orderId, IEnumerable<MetaDataEntry> metaDataEntries)
        {
            var targetKeys = new[] {
                "_billing_next_of_kin",
                "_billing_next_of_kin_contacts",
                "_billing_email_of_your_next_of_kin",
                "_billing_next_of_kin_address"
            };

            var lookup = metaDataEntries
                .Where(m => targetKeys.Contains(m.Key))
                .ToDictionary(m => m.Key, m => m.Value);

            return new Contact
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Name = lookup.GetValueOrDefault("_billing_next_of_kin", string.Empty),
                Phone = lookup.GetValueOrDefault("_billing_next_of_kin_contacts", string.Empty),
                Email = lookup.GetValueOrDefault("_billing_email_of_your_next_of_kin", string.Empty),
                Address = lookup.GetValueOrDefault("_billing_next_of_kin_address", string.Empty)
            };
        }

        public static List<ImagesMetadata> ExtractDocumentMetadata(string jsonString)
        {
            var targetKeys = new[] {
                "national_id_front",
                "national_id_back",
                "photo_of_locked_phone",
                "photo_of_applicant"
            };

            try
            {
                var metaDataEntries = JsonConvert.DeserializeObject<List<MetaDataEntry>>(jsonString);

                var results = new List<ImagesMetadata>();

                foreach (var entry in metaDataEntries)
                {
                    if (!targetKeys.Contains(entry.Key))
                        continue;

                    if (string.IsNullOrWhiteSpace(entry.Value) || !entry.Value.TrimStart().StartsWith("{"))
                        continue;

                    try
                    {
                        var parsedValue = JObject.Parse(entry.Value);

                        foreach (var prop in parsedValue.Properties())
                        {
                            var fileInfo = prop.Value.ToObject<JObject>();

                            results.Add(new ImagesMetadata
                            {
                                Id = entry.Id,
                                Key = entry.Key,
                                FileName = prop.Name,
                                Url = fileInfo["url"]?.ToString(),
                                File = fileInfo["file"]?.ToString(),
                                Type = fileInfo["type"]?.ToString(),
                                Size = fileInfo["size"]?.ToObject<int>() ?? 0
                            });
                        }
                    }
                    catch (JsonReaderException)
                    {
                        // Handle bad JSON inside value field (e.g. a non-object value)
                        continue;
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                var foo = jsonString;
                throw;
            }
            
        }

        private List<OrderProduct> MapOrderProducts(JToken value)
        {
            var products = new List<OrderProduct>();
            var jarray = value["line_items"] as JArray ?? new JArray(); ;

            foreach (var item in jarray)
            {
                products.Add(new OrderProduct()
                {
                    //OrderId = orderId,
                    ProductName = item["name"]?.Value<string?>() ?? "",
                    ProductColor = item["meta_data"]?.FirstOrDefault(x => x["display_key"]?.ToString() == "Color")?["display_value"]?.ToString(),
                    ProductStorage = item["meta_data"]?.FirstOrDefault(x => x["display_key"]?.ToString() == "Storage")?["display_value"]?.ToString(),
                    ProductRam = item["meta_data"]?.FirstOrDefault(x => x["display_key"]?.ToString() == "RAM")?["display_value"]?.ToString(),
                    Quantity = item["quantity"]?.Value<int?>() ?? 0,
                    ProductId = item["product_id"]?.Value<long?>() ?? 0,
                    Sku = item["sku"]?.Value<string?>() ?? ""
                });
            }

            return products;
        }

        public async Task<List<int>> SyncUpdateMetaDataWooOrders()
        {
            var updatedOrders = new List<int>();
            // Start from last sync date or fallback to 3 days ago
            var iso8601UtcDate = DateTime.UtcNow.AddDays(-7).Date.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var mappedOrders = new List<WooOrder>();
            int page = 1;
            string recordsToSync;

            do
            {
                // Fetch paginated data
                recordsToSync = await SecuredApiGetRequestStringResponse(iso8601UtcDate, page);

                if (!string.IsNullOrWhiteSpace(recordsToSync) && recordsToSync.Trim() != "[]")
                {
                    var values = JArray.Parse(recordsToSync);
                    if (values.Count > 0)
                    {
                        foreach (var value in values)
                        {
                            var mappedFromJson = MapOrderFromWoo(value);
                            mappedOrders.Add(mappedFromJson);
                        }
                    }
                }

                page++;

            } while (!string.IsNullOrWhiteSpace(recordsToSync) && recordsToSync.Trim() != "[]");

            if (mappedOrders.Any())
            {
                foreach (var mappedOrder in mappedOrders)
                {
                    await UpdateMatadata(mappedOrder.OrderID, mappedOrder.MetaData);
                }
            }

            return updatedOrders;
        }

        private async Task UpdateMatadata(long orderID, UserMetaData? metaData)
        {
            if (metaData != null)
            {
                await _wooOrderProductRepository.InsertMetaDataAsync(metaData);
            }
        }
    }
}
