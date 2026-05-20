using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Ranalo.Models;

namespace Ranalo.Services
{
    public class DeviceProcessor : IDeviceProcessor
    {
        private readonly string consumerKey = "Token 8efccf09d4874f88ba2a62f5db8d8efc";

        public async Task<List<LockTransaction>> ProcessBatchesAsync(List<LockTransaction> devices)
        {

            using (var client = new HttpClient())
            {
                const int batchSize = 100;
                int nDevices = devices.Count;
                int numBatches = (int)Math.Ceiling((double)nDevices / batchSize);
                var authToken = Encoding.ASCII.GetBytes(consumerKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("Authorization", consumerKey);

                for (int batchIdx = 0; batchIdx < numBatches; batchIdx++)
                {
                    // 1️⃣ Get current slice
                    var slice = devices.Skip(batchIdx * batchSize).Take(batchSize).ToList();

                    // 2️⃣ Build payload
                    var payload = new
                    {
                        data = slice.Select(d => new
                        {
                            device_id = d.AccountId,
                            auto_lock_date = d.AutoLockDate.ToString("dd/MM/yyyy'T'HH:mm:ss")  // "dd/MM/yyyy"
                        })
                    };

                        string jsonBody = JsonSerializer.Serialize(payload);

                        // 3️⃣ Send PATCH request
                        var request = new HttpRequestMessage(HttpMethod.Patch, "https://app.nuovopay.com/dm/api/v3/devices/unlock.json")
                        {
                            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                        };

                        HttpResponseMessage response = await client.SendAsync(request);
                        string responseText = await response.Content.ReadAsStringAsync();

                        // 4️⃣ Parse JSON result
                        string message = null;
                        bool? success = null;

                        try
                        {
                            using var doc = JsonDocument.Parse(responseText);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("message", out var msgProp))
                                message = msgProp.GetString();
                            else if (root.TryGetProperty("errors", out var errProp))
                                message = errProp.ToString();

                            if (root.TryGetProperty("success", out var successProp))
                                success = successProp.GetBoolean();
                        }
                        catch
                        {
                            message = "Invalid JSON response.";
                        }

                        // 5️⃣ Store result in corresponding rows
                        foreach (var d in slice)
                            d.Result = message;

                        Console.WriteLine($"Batch {batchIdx + 1}/{numBatches}: success={success}, message={message}");
                }

            }
            return devices;
        }

        public async Task<LockTransaction> ProcessSingleAsync(LockTransaction device)
        {

            using (var client = new HttpClient())
            {
                var authToken = Encoding.ASCII.GetBytes(consumerKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("Authorization", consumerKey);

                    // 2️⃣ Build payload
                    var payload = new
                    {
                        data = new
                        {
                            device_id = device.AccountId,
                            auto_lock_date = device.AutoLockDate.ToString("dd/MM/yyyy'T'HH:mm:ss")  // "dd/MM/yyyy"
                        }
                    };

                    string jsonBody = JsonSerializer.Serialize(payload);

                    // 3️⃣ Send PATCH request
                    var request = new HttpRequestMessage(HttpMethod.Patch, "https://app.nuovopay.com/dm/api/v3/devices/unlock.json")
                    {
                        Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                    };

                    HttpResponseMessage response = await client.SendAsync(request);
                    string responseText = await response.Content.ReadAsStringAsync();

                    // 4️⃣ Parse JSON result
                    string message = null;
                    bool? success = null;

                    try
                    {
                        using var doc = JsonDocument.Parse(responseText);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("message", out var msgProp))
                            message = msgProp.GetString();
                        else if (root.TryGetProperty("errors", out var errProp))
                            message = errProp.ToString();

                        if (root.TryGetProperty("success", out var successProp))
                            success = successProp.GetBoolean();
                    }
                    catch
                    {
                        message = "Invalid JSON response.";
                    }

                    // 5️⃣ Store result in corresponding rows
                    device.Result = message;
                }
            return device;
        }
    }
}
