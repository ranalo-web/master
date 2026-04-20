using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using Ranalo.SumsungKnox.Models;
using Azure;

namespace Ranalo.SumsungKnox
{
    public class KnoxGuardClient : IKnoxGuardClient
    {
        private readonly HttpClient _httpClient;
        private readonly IKnoxTokenProvider _tokenProvider;

        public KnoxGuardClient(
            HttpClient httpClient,
            IKnoxTokenProvider tokenProvider)
        {
            _httpClient = httpClient;
            _tokenProvider = tokenProvider;

            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async Task PrepareHeadersAsync()
        {
            var token = await _tokenProvider.GetTokenAsync();

            _httpClient.DefaultRequestHeaders.Remove("x-knox-apitoken");
            _httpClient.DefaultRequestHeaders.Add("x-knox-apitoken", token);

            _httpClient.DefaultRequestHeaders.Remove("x-knox-transactionId");
            _httpClient.DefaultRequestHeaders.Add("x-knox-transactionId", Guid.NewGuid().ToString("N"));
        }

        private async Task<HttpResponseMessage> PostAsync(string endpoint, object body)
        {
            await PrepareHeadersAsync();

            var json = JsonSerializer.Serialize(body, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await Task.Delay(3000);
            var response = await _httpClient.PostAsync(endpoint, content);
            //var responseContent = await response.Content.ReadAsStringAsync();
            //response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return response;
        }

        private async Task<TResponse> PostAsync<TResponse>(string endpoint, object body)
        {
            await PrepareHeadersAsync();
            await Task.Delay(3000);

            var response = await _httpClient.PostAsJsonAsync(endpoint, body, JsonOptions);
            var responseContent = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
                   ?? throw new InvalidOperationException("Failed to deserialize response.");
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public Task<HttpResponseMessage> ApproveDeviceAsync(ApproveDeviceRequest request)
            => PostAsync("/kcs/v1.1/kg/devices/approve", request);

        public Task<HttpResponseMessage> ExecuteDeviceActionsAsync(DeviceActionsRequest request)
            => PostAsync("/kcs/v1.1/kg/devices/actions", request);

        public Task<ListDevicesResponse> ListDevicesAsync(ListDevicesRequest request)
            => PostAsync<ListDevicesResponse>("/kcs/v1.1/kg/devices/list", request);

        public Task<HttpResponseMessage> UnlockDeviceAsync(UnlockDeviceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ObjectId) &&
                string.IsNullOrWhiteSpace(request.DeviceUid) &&
                string.IsNullOrWhiteSpace(request.ApproveId))
            {
                throw new ArgumentException(
                    "One of ObjectId, DeviceUid, or ApproveId must be provided.");
            }

            return PostAsync("/kcs/v1.1/kg/devices/unlock", request);
        }

        public Task<HttpResponseMessage> SetBlinkingReminderAsync(SetBlinkingReminderRequest request)
            => PostAsync("/kcs/v1.1/kg/devices/blinkingReminder", request);

        public Task<HttpResponseMessage> CompleteDeviceAsync(CompleteDeviceRequest request)
            => PostAsync("/kcs/v1.1/kg/devices/complete", request);

        public Task<HttpResponseMessage> DeleteDeviceAsync(DeleteDeviceRequest request)
            => PostAsync("/kcs/v1.1/kg/devices/delete", request);

        public Task<HttpResponseMessage> LockDeviceAsync(LockDeviceRequest request)
            => PostAsync("/kcs/v1.1/kg/devices/lock", request);

        public Task<HttpResponseMessage> SendMessageAsync(SendMessageRequest request)
            => PostAsync("/kcs/v1.1/kg/devices/sendMessage", request);

        public async Task<HttpResponseMessage> GetDeviceInfoAsync(string deviceId)
        {
            await PrepareHeadersAsync();

            var response = await _httpClient.GetAsync($"/kcs/v1.1/kg/devices/{deviceId}");
            response.EnsureSuccessStatusCode();
            return response;
        }
    }
}
