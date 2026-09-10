using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Ranalo.PayTrigger.Models;

namespace Ranalo.PayTrigger
{
    public class PayTriggerClient : IPayTriggerClient
    {
        private readonly HttpClient _httpClient;
        private readonly PayTriggerSettings _settings;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public PayTriggerClient(HttpClient httpClient, IOptions<PayTriggerSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;

            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public Task<PreEnrollImeiResponse> PreEnrollImeiAsync(List<ImeiEnrollItem> items, bool preLockFlag)
        {
            var request = new PreEnrollImeiRequest
            {
                ImeiInfo = JsonSerializer.Serialize(items, JsonOptions),
                PreLockFlag = preLockFlag,
                ApiKey = _settings.ApiKey
            };

            return PostAsync<PreEnrollImeiResponse>("api/partner/lock/v1/imei/input", request);
        }

        public Task<PayTriggerApiResponse> UpdateRepayInfoAsync(UpdateRepayInfoRequest request)
        {
            request.RelatedMerchant = _settings.ApiKey;
            return PostAsync<PayTriggerApiResponse>("api/partner/lock/v1/updateRepayInfo", request);
        }

        public Task<PayTriggerApiResponse> RemoveLockAsync(RemoveLockRequest request)
        {
            request.ApiKey = _settings.ApiKey;
            return PostAsync<PayTriggerApiResponse>("api/partner/lock/v1/removeLock", request);
        }

        public Task<FindLockStateResponse> FindLockStateAsync(FindLockStateRequest request)
        {
            request.ApiKey = _settings.ApiKey;
            return PostAsync<FindLockStateResponse>("api/partner/lock/v1/findLockState", request);
        }

        public Task<PayTriggerApiResponse> SendPushInfoAsync(SendPushInfoRequest request)
        {
            request.ApiKey = _settings.ApiKey;
            return PostAsync<PayTriggerApiResponse>("api/partner/push/v1/sendPushInfo", request);
        }

        // Serializes the request once, derives the sign content from that
        // exact JSON (see PayTriggerSigner) so the signed content and the
        // sent body can never drift apart, and posts it with the resulting
        // "sign" header.
        private async Task<TResponse> PostAsync<TResponse>(string relativePath, object request)
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var sign = PayTriggerSigner.SignJson(json, _settings.ApiKey);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, relativePath)
            {
                Content = content
            };
            httpRequest.Headers.Add("sign", sign);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseJson = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<TResponse>(responseJson, JsonOptions)
                   ?? throw new InvalidOperationException("Failed to deserialize PayTrigger response.");
        }
    }
}
