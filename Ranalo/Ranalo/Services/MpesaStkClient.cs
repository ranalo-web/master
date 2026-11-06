using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public record StkStart(bool Sent, string Message, string? CheckoutRequestId = null);

    public interface IMpesaStkClient
    {
        bool Ready { get; }

        // Sends the PIN prompt for paybill 4090703, account = accountNo.
        // The payment itself arrives later on the callback.
        Task<StkStart> RequestAsync(long accountNo, string phone, decimal amount);
    }

    // M-Pesa Express (STK push) via Safaricom Daraja. See SafaricomStkSettings.
    public class MpesaStkClient : IMpesaStkClient
    {
        private static readonly SemaphoreSlim TokenGate = new(1, 1);
        private static string? _token;
        private static DateTime _tokenExpiresUtc;

        private readonly HttpClient _http;
        private readonly SafaricomStkSettings _settings;
        private readonly IStkRequestRepository _requests;
        private readonly ILogger<MpesaStkClient> _logger;

        public MpesaStkClient(HttpClient http, IOptions<SafaricomStkSettings> options,
            IStkRequestRepository requests, ILogger<MpesaStkClient> logger)
        {
            _http = http;
            _settings = options.Value;
            _requests = requests;
            _logger = logger;
        }

        public bool Ready => _settings.Ready;

        public async Task<StkStart> RequestAsync(long accountNo, string phone, decimal amount)
        {
            if (!Ready) return new(false, "Paying from the app isn't switched on yet. Use Pay Bill 4090703.");

            var msisdn = StkRules.Phone(phone);
            if (msisdn == null) return new(false, "That phone number isn't a Safaricom number we can send a prompt to.");

            var whole = StkRules.Amount(amount);
            if (whole == null) return new(false, "Enter an amount of at least Ksh 1.");

            var timestamp = StkRules.Timestamp(DateTime.UtcNow);
            var body = new
            {
                BusinessShortCode = _settings.ShortCode,
                Password = StkRules.Password(_settings.ShortCode, _settings.Passkey!, timestamp),
                Timestamp = timestamp,
                TransactionType = "CustomerPayBillOnline",
                Amount = whole.Value,
                PartyA = msisdn,
                PartyB = _settings.ShortCode,
                PhoneNumber = msisdn,
                CallBackURL = $"{_settings.CallbackBaseUrl!.TrimEnd('/')}/api/kose/stk/{_settings.CallbackToken}/callback",
                AccountReference = StkRules.AccountReference(accountNo),
                TransactionDesc = "Phone pay",
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl.TrimEnd('/')}/mpesa/stkpush/v1/processrequest")
                {
                    Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());

                using var response = await _http.SendAsync(request);
                var reply = await response.Content.ReadAsStringAsync();
                using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(reply) ? "{}" : reply);
                var root = json.RootElement;

                string? Field(string name) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) ? v.ToString() : null;

                if (!response.IsSuccessStatusCode || Field("ResponseCode") != "0" || string.IsNullOrWhiteSpace(Field("CheckoutRequestID")))
                {
                    _logger.LogWarning("STK push for {Account} refused: HTTP {Status} {Reply}", accountNo, (int)response.StatusCode, reply);
                    return new(false, Field("errorMessage") ?? Field("ResponseDescription") ?? "M-Pesa didn't accept the request. Try again or use Pay Bill 4090703.");
                }

                var checkout = Field("CheckoutRequestID")!;
                await _requests.AddAsync(new StkRequest
                {
                    CheckoutRequestId = checkout,
                    MerchantRequestId = Field("MerchantRequestID"),
                    AccountNo = accountNo,
                    Phone = msisdn,
                    Amount = whole.Value,
                    CreatedAtUtc = DateTime.UtcNow,
                });

                _logger.LogInformation("STK push sent: Account={Account} Amount={Amount} Checkout={Checkout}", accountNo, whole, checkout);
                return new(true, Field("CustomerMessage") ?? "Check your phone and enter your M-Pesa PIN.", checkout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "STK push for {Account} failed", accountNo);
                return new(false, "Couldn't reach M-Pesa. Try again or use Pay Bill 4090703.");
            }
        }

        // Daraja tokens last an hour; one is shared and renewed 5 minutes early.
        private async Task<string> TokenAsync()
        {
            await TokenGate.WaitAsync();
            try
            {
                if (_token != null && DateTime.UtcNow < _tokenExpiresUtc) return _token;

                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_settings.BaseUrl.TrimEnd('/')}/oauth/v1/generate?grant_type=client_credentials");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ConsumerKey}:{_settings.ConsumerSecret}")));

                using var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                _token = json.RootElement.GetProperty("access_token").GetString();
                var seconds = int.TryParse(json.RootElement.TryGetProperty("expires_in", out var e) ? e.ToString() : null, out var s) ? s : 3599;
                _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, seconds - 300));
                return _token!;
            }
            finally
            {
                TokenGate.Release();
            }
        }
    }
}
