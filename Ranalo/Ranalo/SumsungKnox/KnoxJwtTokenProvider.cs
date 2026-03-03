using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using Ranalo.SumsungKnox.Models;
using knoxAPIUtility;
using System.Diagnostics;

namespace Ranalo.SumsungKnox
{
    public class KnoxJwtTokenProvider : IKnoxTokenProvider
    {
        private readonly HttpClient _httpClient;
        private readonly KnoxSettings _settings;

        private string? _cachedToken;
        private DateTime _expiryUtc = DateTime.MinValue;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public KnoxJwtTokenProvider(
            HttpClient httpClient,
            IOptions<KnoxSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;
        }

        public async Task<string> GetTokenAsync()
        {
            if (_cachedToken != null &&
                DateTime.UtcNow < _expiryUtc.AddMinutes(-2))
            {
                return _cachedToken;
            }

            await _refreshLock.WaitAsync();

            try
            {
                if (_cachedToken != null &&
                    DateTime.UtcNow < _expiryUtc.AddMinutes(-2))
                {
                    return _cachedToken;
                }

                var token = await RequestNewTokenAsync();

                _cachedToken = token;
                _expiryUtc = DateTime.UtcNow.AddMinutes(30);

                return token;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task<string> RequestNewTokenAsync()
        {
            // 1️⃣ Generate signed client identifier JWT
            var fullPath = GetFullPath(_settings.KeysFilePath);

            Console.WriteLine("BaseDirectory: " + AppContext.BaseDirectory);
            Console.WriteLine("Relative path: " + _settings.KeysFilePath);
            Console.WriteLine("Full path: " + fullPath);
            Console.WriteLine("File exists: " + File.Exists(fullPath));

            var signedClientId =
                KnoxTokenUtility.generateSignedClientIdentifierJWT(
                    fullPath,
                    _settings.ClientIdentifier);

            await Task.Delay(3000);

            Console.WriteLine($"Signed client is ready {signedClientId}. and Full Path is there {fullPath}");

            var payload = new
            {
                base64EncodedStringPublicKey =
                    _settings.Base64EncodedStringPublicKey,
                clientIdentifierJwt = signedClientId,
                validityForAccessTokenInMinutes = 30
            };

            var json = JsonSerializer.Serialize(payload);
            using var content =
                new StringContent(json, Encoding.UTF8, "application/json");

            Console.WriteLine("Content of post: " + content);
            var response =
                await _httpClient.PostAsync("/ams/v1/users/accesstoken", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(
                    $"Knox token request failed: {response.StatusCode} - {error}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            var tokenResponse =
                JsonSerializer.Deserialize<AccessTokenResponse>(
                    jsonResponse,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })!;

            // 2️⃣ Sign access token
            var signedAccessToken =
                KnoxTokenUtility.generateSignedAccessTokenJWT(
                    fullPath,
                    tokenResponse.AccessToken);

            return signedAccessToken;
        }

        private string GetFullPath(string relativePath)
        {
            return Path.Combine(AppContext.BaseDirectory, relativePath);
        }
    }
}
