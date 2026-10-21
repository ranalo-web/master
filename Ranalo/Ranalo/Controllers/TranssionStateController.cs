using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.PayTrigger;
using Ranalo.PayTrigger.Models;

namespace Ranalo.Controllers
{
    // What Transsion (PayTrigger) says about a phone right now -- read only.
    // For checking a phone that's online but not locked, or not unlocking.
    [LoadUserSettingsFromCookie]
    public class TranssionStateController : Controller
    {
        private readonly IPayTriggerClient _payTrigger;

        public TranssionStateController(IPayTriggerClient payTrigger)
        {
            _payTrigger = payTrigger;
        }

        [HttpGet]
        [Route("enrolments/transsion-state/{imei}")]
        public async Task<IActionResult> State(string imei)
        {
            var settings = (User)HttpContext.Items["UserSettings"]!;
            if (settings.RoleId is not (UserRole.Admin or UserRole.Approver))
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrWhiteSpace(imei) || imei.Length > 20 || !imei.All(char.IsDigit))
            {
                return BadRequest("Give a phone's IMEI.");
            }

            var response = await _payTrigger.FindLockStateAsync(new FindLockStateRequest { Imei = imei });
            var d = response.Data;
            // Transsion sends seconds (milliseconds on some calls), as RecoveryService reads them.
            static string? When(long? t) => t is > 0
                ? DateTimeOffset.FromUnixTimeSeconds(t.Value > 99999999999 ? t.Value / 1000 : t.Value).UtcDateTime.ToString("dd MMM yyyy HH:mm 'UTC'")
                : null;

            return Json(new
            {
                imei,
                response.Code,
                response.Message,
                lockState = d?.LockState,
                lockStateMeans = d?.LockState switch
                {
                    0 => "unregistered", 1000 => "registered (not activated)", 2000 => "ready to activate",
                    3000 => "active", 5000 => "removable", _ => "unknown"
                },
                mobileStatus = d?.MobileStatus,
                mobileStatusMeans = d?.MobileStatus switch { 1000 => "locked", 2000 => "unlocked", _ => "unknown" },
                d?.ServerState,
                d?.ServerLockStatus,
                activated = When(d?.ActiveTime),
                lockExpires = When(d?.Expiration),
                lastConnected = When(d?.LastConnectTime),
                d?.Model,
                d?.ApkVersion
            });
        }
    }
}
