using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Ranalo.Models;

namespace Ranalo.Services
{
    public class DeviceProcessor : IDeviceProcessor
    {
        private readonly string consumerKey = "Token 8efccf09d4874f88ba2a62f5db8d8efc";

        public async Task<List<LockTransaction>> ProcessBatchesAsync(
     List<LockTransaction> devices,
     ILogger logger)
        {
            const int batchSize = 100;
            const string endpoint =
                "https://app.nuovopay.com/dm/api/v3/devices/unlock.json";

            if (devices == null)
            {
                logger.LogError(
                    "ProcessBatchesAsync was called with a null device list.");

                throw new ArgumentNullException(nameof(devices));
            }

            var totalDevices =
                devices.Count;

            var numBatches =
                (int)Math.Ceiling(
                    (double)totalDevices / batchSize);

            logger.LogInformation(
                "Starting device batch processing. " +
                "Devices={DeviceCount}, BatchSize={BatchSize}, Batches={BatchCount}",
                totalDevices,
                batchSize,
                numBatches);

            using var client =
                new HttpClient();

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));

            client.DefaultRequestHeaders.Add(
                "Authorization",
                consumerKey);

            for (var batchIdx = 0;
                 batchIdx < numBatches;
                 batchIdx++)
            {
                var batchNumber =
                    batchIdx + 1;

                var slice =
                    devices
                        .Skip(batchIdx * batchSize)
                        .Take(batchSize)
                        .ToList();

                logger.LogInformation(
                    "Starting batch {BatchNumber}/{BatchCount}. " +
                    "DeviceCount={DeviceCount}",
                    batchNumber,
                    numBatches,
                    slice.Count);

                try
                {
                    var payload =
                        new
                        {
                            data =
                                slice.Select(
                                    d => new
                                    {
                                        device_id =
                                            d.AccountId,

                                        auto_lock_date =
                                            d.AutoLockDate.ToString(
                                                "dd/MM/yyyy'T'HH:mm:ss")
                                    })
                        };

                    var jsonBody =
                        JsonSerializer.Serialize(
                            payload);

                    logger.LogDebug(
                        "Built payload for batch {BatchNumber}/{BatchCount}. " +
                        "PayloadLength={PayloadLength}",
                        batchNumber,
                        numBatches,
                        jsonBody.Length);

                    using var request =
                        new HttpRequestMessage(
                            HttpMethod.Patch,
                            endpoint)
                        {
                            Content =
                                new StringContent(
                                    jsonBody,
                                    Encoding.UTF8,
                                    "application/json")
                        };

                    logger.LogInformation(
                        "Sending PATCH request for batch " +
                        "{BatchNumber}/{BatchCount} to {Endpoint}",
                        batchNumber,
                        numBatches,
                        endpoint);

                    using var response =
                        await client.SendAsync(
                            request);

                    var responseText =
                        await response.Content.ReadAsStringAsync();

                    logger.LogInformation(
                        "Received response for batch " +
                        "{BatchNumber}/{BatchCount}. " +
                        "StatusCode={StatusCode}, ReasonPhrase={ReasonPhrase}",
                        batchNumber,
                        numBatches,
                        (int)response.StatusCode,
                        response.ReasonPhrase);

                    string? message = null;
                    bool? success = null;

                    try
                    {
                        using var doc =
                            JsonDocument.Parse(
                                responseText);

                        var root =
                            doc.RootElement;

                        if (root.TryGetProperty(
                                "message",
                                out var msgProp))
                        {
                            message =
                                msgProp.GetString();
                        }
                        else if (root.TryGetProperty(
                                     "errors",
                                     out var errProp))
                        {
                            message =
                                errProp.ToString();
                        }

                        if (root.TryGetProperty(
                                "success",
                                out var successProp))
                        {
                            success =
                                successProp.ValueKind ==
                                JsonValueKind.True
                                ? true
                                : successProp.ValueKind ==
                                  JsonValueKind.False
                                    ? false
                                    : null;
                        }
                    }
                    catch (JsonException ex)
                    {
                        message =
                            "Invalid JSON response.";

                        logger.LogWarning(
                            ex,
                            "Could not parse API response for batch " +
                            "{BatchNumber}/{BatchCount}. " +
                            "StatusCode={StatusCode}, ResponseLength={ResponseLength}",
                            batchNumber,
                            numBatches,
                            (int)response.StatusCode,
                            responseText?.Length ?? 0);
                    }

                    foreach (var device in slice)
                    {
                        device.Result =
                            message;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        logger.LogInformation(
                            "Completed batch {BatchNumber}/{BatchCount}. " +
                            "Success={Success}, Message={Message}, Devices={DeviceCount}",
                            batchNumber,
                            numBatches,
                            success,
                            message,
                            slice.Count);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Batch {BatchNumber}/{BatchCount} returned " +
                            "an unsuccessful HTTP response. " +
                            "StatusCode={StatusCode}, Success={Success}, " +
                            "Message={Message}," +
                            "Body={jsonBody}",
                            batchNumber,
                            numBatches,
                            (int)response.StatusCode,
                            success,
                            message,
                            jsonBody);
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError(
                        ex,
                        "HTTP request failed for batch " +
                        "{BatchNumber}/{BatchCount}. Devices={DeviceCount}",
                        batchNumber,
                        numBatches,
                        slice.Count);

                    foreach (var device in slice)
                    {
                        device.Result =
                            $"HTTP request failed: {ex.Message}";
                    }
                }
                catch (TaskCanceledException ex)
                {
                    logger.LogError(
                        ex,
                        "Request timed out or was cancelled for batch " +
                        "{BatchNumber}/{BatchCount}.",
                        batchNumber,
                        numBatches);

                    foreach (var device in slice)
                    {
                        device.Result =
                            "Request timed out or was cancelled.";
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Unexpected error processing batch " +
                        "{BatchNumber}/{BatchCount}. Devices={DeviceCount}",
                        batchNumber,
                        numBatches,
                        slice.Count);

                    foreach (var device in slice)
                    {
                        device.Result =
                            $"Unexpected error: {ex.Message}";
                    }
                }
            }

            var successful =
                devices.Count(x =>
                    !string.IsNullOrWhiteSpace(x.Result) &&
                    !x.Result.Contains(
                        "failed",
                        StringComparison.OrdinalIgnoreCase) &&
                    !x.Result.Contains(
                        "error",
                        StringComparison.OrdinalIgnoreCase));

            logger.LogInformation(
                "Finished device batch processing. " +
                "TotalDevices={TotalDevices}, Batches={BatchCount}, " +
                "Processed={ProcessedCount}",
                totalDevices,
                numBatches,
                devices.Count);

            return devices;
        }

        public async Task<LockTransaction> ProcessSingleAsync(
    LockTransaction device,
    ILogger logger)
        {
            const string endpoint =
                "https://app.nuovopay.com/dm/api/v3/devices/unlock.json";

            if (device == null)
            {
                logger.LogError(
                    "ProcessSingleAsync was called with a null device.");

                throw new ArgumentNullException(nameof(device));
            }

            logger.LogInformation(
                "Starting single device processing. AccountId={AccountId}, AutoLockDate={AutoLockDate}",
                device.AccountId,
                device.AutoLockDate);

            try
            {
                using var client =
                    new HttpClient();

                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue(
                        "application/json"));

                client.DefaultRequestHeaders.Add(
                    "Authorization",
                    consumerKey);

                var payload =
                    new
                    {
                        data =
                            new
                            {
                                device_id =
                                    device.AccountId,

                                auto_lock_date =
                                    device.AutoLockDate.ToString(
                                        "dd/MM/yyyy'T'HH:mm:ss")
                            }
                    };

                var jsonBody =
                    JsonSerializer.Serialize(
                        payload);

                logger.LogDebug(
                    "Built request payload for AccountId={AccountId}. PayloadLength={PayloadLength}",
                    device.AccountId,
                    jsonBody.Length);

                using var request =
                    new HttpRequestMessage(
                        HttpMethod.Patch,
                        endpoint)
                    {
                        Content =
                            new StringContent(
                                jsonBody,
                                Encoding.UTF8,
                                "application/json")
                    };

                logger.LogInformation(
                    "Sending PATCH request for AccountId={AccountId} to {Endpoint}",
                    device.AccountId,
                    endpoint);

                using var response =
                    await client.SendAsync(
                        request);

                var responseText =
                    await response.Content.ReadAsStringAsync();

                logger.LogInformation(
                    "Received response for AccountId={AccountId}. StatusCode={StatusCode}, ReasonPhrase={ReasonPhrase}",
                    device.AccountId,
                    (int)response.StatusCode,
                    response.ReasonPhrase);

                string? message = null;
                bool? success = null;

                try
                {
                    using var doc =
                        JsonDocument.Parse(
                            responseText);

                    var root =
                        doc.RootElement;

                    if (root.TryGetProperty(
                            "message",
                            out var msgProp))
                    {
                        message =
                            msgProp.GetString();
                    }
                    else if (root.TryGetProperty(
                                 "errors",
                                 out var errProp))
                    {
                        message =
                            errProp.ToString();
                    }

                    if (root.TryGetProperty(
                            "success",
                            out var successProp))
                    {
                        success =
                            successProp.ValueKind switch
                            {
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => null
                            };
                    }
                }
                catch (JsonException ex)
                {
                    message =
                        "Invalid JSON response.";

                    logger.LogWarning(
                        ex,
                        "Could not parse API response for AccountId={AccountId}. " +
                        "StatusCode={StatusCode}, ResponseLength={ResponseLength}",
                        device.AccountId,
                        (int)response.StatusCode,
                        responseText?.Length ?? 0);
                }

                device.Result =
                    message;

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation(
                        "Completed device processing. AccountId={AccountId}, Success={Success}, Message={Message}",
                        device.AccountId,
                        success,
                        message);
                }
                else
                {
                    logger.LogWarning(
                        "Device processing returned unsuccessful HTTP response. " +
                        "AccountId={AccountId}, StatusCode={StatusCode}, Success={Success}, Message={Message}",
                        device.AccountId,
                        (int)response.StatusCode,
                        success,
                        message);
                }
            }
            catch (HttpRequestException ex)
            {
                device.Result =
                    $"HTTP request failed: {ex.Message}";

                logger.LogError(
                    ex,
                    "HTTP request failed while processing AccountId={AccountId}",
                    device.AccountId);
            }
            catch (TaskCanceledException ex)
            {
                device.Result =
                    "Request timed out or was cancelled.";

                logger.LogError(
                    ex,
                    "Request timed out or was cancelled while processing AccountId={AccountId}",
                    device.AccountId);
            }
            catch (Exception ex)
            {
                device.Result =
                    $"Unexpected error: {ex.Message}";

                logger.LogError(
                    ex,
                    "Unexpected error while processing AccountId={AccountId}",
                    device.AccountId);
            }

            logger.LogInformation(
                "Finished ProcessSingleAsync for AccountId={AccountId}. Result={Result}",
                device.AccountId,
                device.Result);

            return device;
        }
    }
}
