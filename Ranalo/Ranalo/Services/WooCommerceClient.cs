using System.Text.Json;
using System.Text;

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
    }
}
