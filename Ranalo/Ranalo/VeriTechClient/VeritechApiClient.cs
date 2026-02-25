using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Ranalo.VeriTechClient.Models;

namespace Ranalo.VeriTechClient
{
    public class VeritechApiClient : IVeritechApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public VeritechApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        private async Task<T> SendAsync<T>(HttpRequestMessage request)
        {
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Veritech API Error: {response.StatusCode} - {content}");
            }

            return JsonSerializer.Deserialize<T>(content, _jsonOptions)!;
        }

        public async Task<GetDevicesResponse> GetDevicesAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "devices");
            return await SendAsync<GetDevicesResponse>(request);
        }

        public async Task<CreateDeviceResponse> UploadDevicesAsync(List<string> devices)
        {
            var payload = new { devices };

            var request = new HttpRequestMessage(HttpMethod.Post, "devices/upload")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };

            return await SendAsync<CreateDeviceResponse>(request);
        }

        public async Task<DeleteDeviceResponse> DeleteDevicesAsync(List<string> devices)
        {
            var payload = new { devices };

            var request = new HttpRequestMessage(HttpMethod.Delete, "devices/delete")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };

            return await SendAsync<DeleteDeviceResponse>(request);
        }

        public async Task<SuccessTransactionStatusResponse> GetTransactionStatusAsync(string transactionId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"devices/transaction-status/{transactionId}");

            return await SendAsync<SuccessTransactionStatusResponse>(request);
        }

        public async Task<LockDeviceResponse> LockDeviceAsync(LockDeviceInput input)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "knox-guard/lock-device")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(input),
                    Encoding.UTF8,
                    "application/json")
            };

            return await SendAsync<LockDeviceResponse>(request);
        }

        public async Task<UnlockDeviceResponse> UnlockDeviceAsync(UnlockDeviceInput input)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "knox-guard/unlock-device")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(input),
                    Encoding.UTF8,
                    "application/json")
            };

            return await SendAsync<UnlockDeviceResponse>(request);
        }
    }
}
