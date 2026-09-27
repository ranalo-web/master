using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;
using Ranalo.Services.Helpers;
using Ranalo.Woocommece.Api.DataStore;
using System.Text.Json;

namespace Ranalo.Controllers
{
    // Receives PayTrigger's device-status-change webhook (section 5 of the
    // PayTrigger Partner API docs: "设备状态变化回传通知合作方API") -- pushed
    // whenever a Transsion device we manage actually activates, locks,
    // unlocks, or gets removed, plus a separate notification when a strong-
    // restriction lock instruction couldn't take effect.
    //
    // This is a server-to-server callback, not a logged-in-user request --
    // no [LoadUserSettingsFromCookie], no cookie session. Authenticity is
    // verified via the same HMAC "sign" header scheme used for our own
    // outbound PayTrigger requests (PayTriggerSigner), using our apiKey as
    // the shared secret.
    //
    // PayTrigger requires an exact {"code":200,"message":"Success"} body to
    // acknowledge -- anything else is treated as a failure and retried on a
    // compensation schedule until it succeeds or hits their retry limit.
    public class PayTriggerWebhookController : Controller
    {
        private readonly IKosePaymentsRepository _kosePaymentsRepository;
        private readonly PayTriggerSettings _settings;
        private readonly ILogger<PayTriggerWebhookController> _logger;

        public PayTriggerWebhookController(
            IKosePaymentsRepository kosePaymentsRepository,
            IOptions<PayTriggerSettings> options,
            ILogger<PayTriggerWebhookController> logger)
        {
            _kosePaymentsRepository = kosePaymentsRepository;
            _settings = options.Value;
            _logger = logger;
        }

        [HttpPost]
        [Route("paytrigger/device-status-callback")]
        public async Task<IActionResult> DeviceStatusCallback()
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            var providedSign = Request.Headers["sign"].ToString();

            if (!PayTriggerSigner.VerifyJson(rawBody, _settings.ApiKey, providedSign))
            {
                _logger.LogWarning("PayTrigger webhook: signature verification failed, rejecting.");
                return StatusCode(401, new { code = 40000, message = "Sign error" });
            }

            DeviceStatusCallback callback;
            try
            {
                callback = JsonSerializer.Deserialize<DeviceStatusCallback>(rawBody)
                           ?? throw new InvalidOperationException("Empty callback body.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PayTrigger webhook: failed to parse callback body.");
                return BadRequest(new { code = 400, message = "bad request" });
            }

            try
            {
                if (callback.NotifyType == 4000)
                {
                    // Strong-restriction lock instruction exceeded/blocked --
                    // no device state change to reflect, just surface it.
                    _logger.LogWarning(
                        "PayTrigger strong-restriction notice for deviceTag={DeviceTag} orderNum={OrderNum} imei={Imei}: {Tip}",
                        callback.DeviceTag, callback.OrderNum, callback.Imei, callback.Tip);

                    return Ok(new { code = 200, message = "Success" });
                }

                if (string.IsNullOrEmpty(callback.Imei))
                {
                    _logger.LogWarning("PayTrigger webhook: callback has no imei, cannot match a device. deviceTag={DeviceTag}", callback.DeviceTag);
                    return Ok(new { code = 200, message = "Success" });
                }

                var device = await _kosePaymentsRepository.GetDeviceByImeiAsync(callback.Imei);

                if (device == null)
                {
                    // Not one of ours (or a stale notification for a device
                    // we never created) -- nothing to retry for, acknowledge
                    // and move on.
                    _logger.LogWarning("PayTrigger webhook: no local Device found for imei={Imei}", callback.Imei);
                    return Ok(new { code = 200, message = "Success" });
                }

                var isLocked = callback.MobileStatus == 1000;
                device.Locked = isLocked;
                device.LockType = isLocked ? "complete" : "unlocked";

                if (callback.Expiration.HasValue)
                {
                    var expirationMs = callback.Expiration.Value * 1000;
                    device.NextLockDate = TimestampHelper.FormatDateOnly(expirationMs);
                    device.NextLockDateIsoFormat = TimestampHelper.FormatRelockTimestamp(expirationMs);
                }

                if (callback.State.HasValue)
                {
                    device.IsActivated = callback.State.Value >= 3000;
                    device.EnrollmentStatus = callback.State.Value switch
                    {
                        3000 => "Completed",
                        5000 => "Removed",
                        _ => "Pending"
                    };
                }

                if (callback.NotifyType == 2000)
                {
                    device.Status = "removed";
                }

                await _kosePaymentsRepository.UpdateDeviceToDatabaseAsync(device);

                return Ok(new { code = 200, message = "Success" });
            }
            catch (Exception ex)
            {
                // Transient failure (DB, etc.) -- don't ack, let PayTrigger's
                // retry/compensation mechanism try again later.
                _logger.LogError(ex, "PayTrigger webhook: failed to process callback for imei={Imei}", callback.Imei);
                return StatusCode(500, new { code = 500, message = "Server Error" });
            }
        }
    }
}
