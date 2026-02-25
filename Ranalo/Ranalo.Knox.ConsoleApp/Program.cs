

using knoxAPIUtility;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using Auth0.AuthenticationApi.Models;

var cert = "C:\\work\\ranalo\\work\\Ranalo\\Ranalo.Knox.ConsoleApp\\keys\\keys.json";
var clientId = "eyJhbGciOiJIUzUxMiJ9.eyJjbGllbnRJZGVudGlmaWVyIjoiMTg4YzIwNmItZDU4NC00MDc2LWI0N2QtM2M0Zjg2OGNkM2U4YTM4NjZjMzItZDcyZC00NTA1LThmOWItM2Q2ZGUwZjIyNzdmIiwiYXR0cjEiOiIxIn0._qas2XwGYNfyyZ7xFO0l4W8mJ9XJspHfUhMRFjN-0RoW3fp7YqxlgZZuFU-kTvfn5LB3jEmaQ65-LPuZLi9VKQ";
var signedClientId = KnoxTokenUtility.generateSignedClientIdentifierJWT(cert, clientId);

try
{
    var base64EncodedStringPublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAlin9YAbWnpXtcnU1P04dXRBQuDlwjsiG9n4ZNVIouyuJqOXziZlmM1XY4Sdm2gxnFBOngEY9eOfW8UV4wfH0znAKOeDcB9grzaHH6JZZTJ1/1gQ8LEFkDBIS2cyDWMoLnyIwV4nE2Yw+rvI4vuvoj3Iyl/f9PYCeMBSc+NpbDTwpidCnDl7H9f3azHd6EGYUpMNcZlN4Q6UIFTSB8jl4sbZd5ZLY2fQeZF3ljBoH/D0fFytaywcJKcrFNzbgWgdrYrmUooM3Ro/R0kgan5zfFMe7/wgHzBKOkmH18xWPPsyXVk+5CDE7Ocxyfu355UyqbIWKRgS4Z36K+cit8nqoMQIDAQAB";

    using var httpClient = new HttpClient();

    var url = "https://eu-kcs-api.samsungknox.com/ams/v1/users/accesstoken";

    var payload = new
    {
        base64EncodedStringPublicKey = base64EncodedStringPublicKey,
        clientIdentifierJwt = signedClientId,
        validityForAccessTokenInMinutes = 30
    };

    var json = JsonSerializer.Serialize(payload);

    using var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await httpClient.PostAsync(url, content);

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Request failed: {response.StatusCode}");
        Console.WriteLine(error);
        return;
    }

    string jsonResponse = await response.Content.ReadAsStringAsync();

    var tokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(
        jsonResponse,
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

    string accessToken = tokenResponse.AccessToken;

    var signedAccessToken = KnoxTokenUtility.generateSignedAccessTokenJWT(cert, accessToken);

    // Approve a device
    try
    {

        var approve = @"{
              ""deviceUid"": ""351065613492352"",
              ""approveId"": ""testapproval1"",
              ""message"": ""Hello world, from API messaging""
            }";

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://eu-kcs-api.samsungknox.com/kcs/v1.1/kg/devices/sendMessage");

        // Required header
        request.Headers.Add("x-knox-apitoken", signedAccessToken);

        request.Content = new StringContent(
            approve,
            Encoding.UTF8,
            "application/json");

        var response2 = await httpClient.SendAsync(request);

        var responseContent = await response2.Content.ReadAsStringAsync();

        if (!response2.IsSuccessStatusCode)
        {
            Console.WriteLine($"Request failed: {response2.StatusCode}");
            Console.WriteLine(responseContent);
            return;
        }

        Console.WriteLine("Success:");

        Console.WriteLine("Signed Access Token:");
        Console.WriteLine(signedAccessToken);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error:");
        Console.WriteLine(ex.Message);
    }
}
catch (Exception ex)
{
    Console.WriteLine("Error:");
    Console.WriteLine(ex.Message);
}


// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
