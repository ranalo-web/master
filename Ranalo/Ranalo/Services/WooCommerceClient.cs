using System.Text.Json;
using System.Text;
using MySqlX.XDevAPI;

namespace Ranalo.Services
{
    public class WooCommerceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _consumerKey;
        private readonly string _consumerSecret;

        public WooCommerceClient(string baseUrl, string consumerKey, string consumerSecret)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _consumerKey = consumerKey;
            _consumerSecret = consumerSecret;

            _httpClient = new HttpClient();
        }

        public async Task<string> UpdateOrderStatusAsync(long orderId, string newStatus = "approved")
        {
            var url = $"{_baseUrl}/orders/{orderId}" +
                      $"?consumer_key={_consumerKey}&consumer_secret={_consumerSecret}";

            var payload = JsonSerializer.Serialize(new { status = newStatus });

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");


            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Order {orderId} status updated to '{newStatus}'");
                return jsonResponse;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to update order {orderId} (status {response.StatusCode}): {error}");
            }
        }

        public async Task<string> UpdateOrderMpesaAsync(long orderId, string newmpesaCode, long metadataId)
        {
            var url = $"{_baseUrl}/orders/{orderId}" +
                      $"?consumer_key={_consumerKey}&consumer_secret={_consumerSecret}";

            var updatePayload = new
            {
                meta_data = new[]
                {
                    new {
                        id = metadataId, // existing meta ID if updating
                        key = "mpesa_deposit_reference",
                        value = newmpesaCode
                    }
                }
            };

            var payload = JsonSerializer.Serialize(updatePayload);

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Order {orderId} status updated to '{newmpesaCode}'");
                return jsonResponse;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to update order {orderId} (status {response.StatusCode}): {error}");
            }
        }
    }
}
