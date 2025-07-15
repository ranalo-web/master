using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Ranalo.Woocommece.Api.Controllers
{
    [Route("api/DataSync")]
    [ApiController]
    public class HomeController : Controller
    {
        //private readonly IHttpContextAccessor _contextAccessor;
        private readonly ISyncService _syncService;
        public HomeController(ISyncService syncService) 
        { 
            _syncService = syncService;
        }
        // GET: HomeController
        [HttpGet]
        [Route("SyncLogs")]
        [ProducesResponseType(typeof(DataSyncLog), 200)]  // Success
        public async Task<IActionResult> SyncLogs()
        {
            var lastLog = await _syncService.GetLastSycnLogDetails();
            if(lastLog != null)
            {
                return Ok();
            }

            return NoContent();
        }

        [HttpGet]
        [Route("SyncWooOrders")]
        [ProducesResponseType(typeof(List<WooOrder>), 200)]  // Success
        public async Task<ActionResult> WooOrders()
        {
            // Start from last sync date or fallback to 3 days ago
            var iso8601UtcDate = DateTime.UtcNow.AddDays(-24).Date.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var lastLog = await _syncService.GetLastSycnLogDetails();
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
                var createdId = await _syncService.CreateOrderAsync(mappedOrders);

                if (createdId == 1)
                {
                    var log = new DataSyncLog
                    {
                        LastOrderCreatedDate = lastSyncDate,
                        Status = SyncStatus.Success,
                        SyncDate = DateTime.UtcNow,
                        Type = SyncType.Orders
                    };

                    await _syncService.LogLastSyncDetails(log);
                }

                foreach (var mappedOrder in mappedOrders)
                {
                    var orderId = await _syncService.UpdateImagesAsync(mappedOrder.OrderID, mappedOrder.ImagesMetadata);

                    Debug.WriteLine(orderId);
                }
            }

            return Ok(mappedOrders);
        }

        [HttpGet]
        [Route("SyncUpdateImagesWooOrders")]
        [ProducesResponseType(typeof(List<int>), 200)]  // Success
        public async Task<IActionResult> UpdateImages()
        {
            var updatedOrders = new List<int>();
            // Start from last sync date or fallback to 3 days ago
            var iso8601UtcDate = DateTime.UtcNow.AddDays(-4).Date.ToString("yyyy-MM-ddTHH:mm:ssZ");

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

            if(mappedOrders.Any())
            {
                foreach (var mappedOrder in mappedOrders)
                {
                    var orderId = await _syncService.UpdateImagesAsync(mappedOrder.OrderID, mappedOrder.ImagesMetadata);

                    updatedOrders.AddRange(orderId);
                }
            }

            return Ok(updatedOrders);
        }


        [HttpGet]
        [Route("SyncPayments")]
        public async Task<IActionResult> KosePayments()
        {
            var allPayments = await SecuredApiGetRequestStringResponse();

            if (string.IsNullOrEmpty(allPayments))
            {
                return NoContent();
            }

            List<MpesaRecord>? records = JsonConvert.DeserializeObject<List<MpesaRecord>>(allPayments);

            if (records == null)
            {
                return NoContent();
            }

            //With these records we need check the last transaction date.
            DateTime? lastSyncDate = null;
            var lastTransactionDate = await _syncService.GetLastTransactionDateAsync();

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
            await _syncService.CreateKoseBatchPaymentAsync(grouped);

            var lastRecord = records.LastOrDefault();

            var logRecord = new SyncPaymentsLog()
            {
                LastPaymentDate = lastRecord.PaymentDateValue,
                LastPaymentId = lastRecord.Id

            };
            await _syncService.LogLastPaymentSyncDetails(logRecord);

            return Ok(grouped);

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
                MpesaDepositRef = value["identity_verification"]?["mpesa_deposit_reference"]?.Value<string?>() ?? "",
                Products = MapOrderProducts(value),
                ImagesMetadata = ExtractDocumentMetadata(value["meta_data"].ToString())
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

        private List<OrderProduct> MapOrderProducts(JToken value)
        {
            var products = new List<OrderProduct>();
            var jarray = value["line_items"] as JArray ?? new JArray(); ;

            foreach (var item in jarray)
            {
                products.Add(new OrderProduct() {
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

        [HttpGet]
        [Route("SyncWooCustomers")]
        [ProducesResponseType(typeof(object), 200)]  // Success
        public ActionResult WooCustomers()
        {
            return Ok();
        }

        [HttpGet]
        [Route("DeviceUnlockPull")]
        [ProducesResponseType(typeof(object), 200)]  // Success
        public async Task<IActionResult> DeviceDetails()
        {
            int? currentPage = 1;
            var currentDevices = new List<Device>();
            while (currentPage != null)
            {
                var result = await SecuredApiGetDeviceLockingRequestStringResponse((int)currentPage);

                var device = JsonConvert.DeserializeObject<LockDevices>(result);

                if(device != null && device.Devices != null)
                {
                    currentDevices.AddRange(device.Devices);
                    currentPage = device.NextPage;
                }
                else
                {
                    currentPage = null;
                }
            }

            var latestDeviceId = await _syncService.GetLatestDeviceId();

            if(latestDeviceId != null)
            {
                currentDevices = currentDevices.Where(x=>x.Id > latestDeviceId.Id).ToList();
            }

            if(currentDevices.Any())
            {
                await _syncService.CreateDevicesToDatabaseAsync(currentDevices);
            }

            return Ok(currentDevices);
        }


        [HttpGet]
        [Route("OrderById")]
        public async Task<IActionResult> WooGetOrderById(int orderId)
        {
            var order = await SecuredApiGetSingleOrderRequestStringResponse(orderId);

            if (string.IsNullOrEmpty(order))
            {
                return NoContent();
            }

            var mappedOrder = new WooOrder();

            if (!string.IsNullOrWhiteSpace(order) && order.Trim() != "[]")
            {
                var value = JToken.Parse(order);

                var mappedFromJson = MapOrderFromWoo(value);
                mappedOrder = mappedFromJson;
            }

            return Ok(mappedOrder);

        }
        private async Task<string> SecuredApiGetRequestStringResponse(string iso8601UtcDate, int page = 1)
        {
            var consumerKey = "ck_9bf5ade6a031f04b53bd31938d462895db40e00c";
            var consumerSecret = "cs_b2d5d61f3eae5093d85b7319905eb5942c614f99";
            var baseUrl = "https://ranalocredit.com/wp-json/wc/v3";
            var client = new HttpClient();
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

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            if (content.Trim() == "[]")
            {
                // Empty result set
                return "";
            }
            return content;
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
            var client = new HttpClient();
            var response = await client.GetAsync(baseUrl);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}

//Edward Guda
//2:56 PM
//### Patch update to the code
//# Initialize variables for pagination
//API_KEY < -"Token 8efccf09d4874f88ba2a62f5db8d8efc"
//base_url < -"https://app.nuovopay.com/dm/api/v1/devices.json"
//all_data < -list()
//page < -1

//# Fetch the first page to determine the column structure
//api < -GET(base_url, add_headers(Authorization = API_KEY),
//           query = list(limit = 100, page = page))

//if (status_code(api) != 200) { stop("Failed to fetch data. HTTP Status: ", status_code(api))}

//api_response < -content(api
