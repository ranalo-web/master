using Ranalo.DataStore;
using Ranalo.Services.Helpers;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.Woocommece.Api.Services;

namespace Ranalo.ScheduledServices
{
    public class ScheduledTaskKnoxDevicesPull : BackgroundService
    {
        private readonly ILogger<ScheduledTaskKnoxDevicesPull> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(120); // Run every 30 min

        public ScheduledTaskKnoxDevicesPull(
            ILogger<ScheduledTaskKnoxDevicesPull> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Device pull Knox started at : {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: perform a database operation
                    _logger.LogInformation("Running scheduled Devices pull Knox task at: {time}", DateTime.UtcNow);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var knoxClient = scope.ServiceProvider.GetRequiredService<IKnoxGuardClient>();
                        var deviceRepository = scope.ServiceProvider.GetRequiredService<IDevicesRepository>();

                        // Every phone in Knox, whatever its status, so our pages
                        // show what Knox says (KnoxStatusRules). Until 10 Nov 2026
                        // only the first 20 "Enrolled" phones were read.
                        var all = await ListAllAsync(knoxClient);
                        if (all != null)
                        {
                            try
                            {
                                var statusRepo = scope.ServiceProvider.GetRequiredService<IKnoxStatusRepository>();
                                // Our phones the list didn't include are looked up one by one
                                // by IMEI (as Recovery's Check in Knox does): on 10 Nov two
                                // phones Knox had (last changed weeks earlier, so at the end
                                // of the list) were missing from the paged read.
                                var found = await LookUpMissingAsync(knoxClient, statusRepo, all);
                                if (found != null)
                                {
                                    all.AddRange(found);
                                    var matched = await statusRepo.SaveAsync(all.Select(Ranalo.Services.KnoxStatusRules.From).ToList(), DateTime.UtcNow);
                                    _logger.LogInformation("Knox status saved: {Knox} phones read ({Looked} looked up by IMEI), {Matched} of ours.",
                                        all.Count, found.Count, matched);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Knox status not saved (has Database/DeviceLock/002 been run?)");
                            }
                        }

                        var inactiveUsers = await DeviceUnlockPull(all?.Where(d => d.Status == "Enrolled").ToList(), deviceRepository);
                        foreach (var device in inactiveUsers)
                        {
                            _logger.LogInformation("Device Updated from Knox: {order}", device.Id);
                            // Possibly send email reminders, clean up data, etc.
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while running scheduled ScheduledTaskKnoxDevicesPull.");
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Shutting down.
                }
            }

            _logger.LogInformation("ScheduledTaskKnoxDevicesPull stopped at: {time}", DateTime.UtcNow);
        }

        // Knox's whole device list, page by page. null = Knox couldn't be read
        // (nothing is marked "Not in Knox" then).
        public const int PageSize = 100;
        public const int MaxPages = 50;

        private async Task<List<DeviceDto>?> ListAllAsync(IKnoxGuardClient knoxClient)
        {
            var all = new List<DeviceDto>();
            var total = 0;
            try
            {
                for (var page = 0; page < MaxPages; page++)
                {
                    var list = await knoxClient.ListDevicesAsync(new ListDevicesRequest
                    {
                        PageNum = page, PageSize = PageSize, SortBy = "updateTime", SortOrder = "descending",
                    });
                    // A page missing = an incomplete list: save nothing, or phones on
                    // the missing page would be marked "Not in Knox".
                    if (list?.DeviceList == null) return null;
                    total = list.TotalCount;
                    all.AddRange(list.DeviceList);
                    // Until Knox's own total: Knox may send fewer than PageSize a page.
                    if (list.DeviceList.Count == 0 || all.Count >= total) break;
                }

                // Fewer than Knox's total is logged; our own phones missing from
                // it are then looked up by IMEI (LookUpMissingAsync).
                var distinct = all.Select(d => d.ObjectId ?? d.DeviceUid ?? d.Imei).Distinct().Count();
                _logger.LogInformation("Knox list read: {Got} of {Total} phones.", distinct, total);
                return all;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading Knox's device list");
                return null;
            }
        }

        // Our phones (sent to Knox, or seen in Knox before) not in the list:
        // searched by IMEI. null = a lookup failed, so nothing is saved this
        // round (a failed lookup must not mark a phone "Not in Knox").
        public const int MaxLookups = 300;

        private async Task<List<DeviceDto>?> LookUpMissingAsync(IKnoxGuardClient knoxClient, IKnoxStatusRepository repo, List<DeviceDto> read)
        {
            var seen = new HashSet<string>(read.SelectMany(d => new[] { d.Imei, d.Imei2 }).Where(i => !string.IsNullOrWhiteSpace(i))!,
                StringComparer.OrdinalIgnoreCase);
            var missing = (await repo.GetTrackedImeisAsync()).Where(i => !seen.Contains(i)).Take(MaxLookups).ToList();
            var found = new List<DeviceDto>();
            foreach (var imei in missing)
            {
                try
                {
                    var list = await knoxClient.ListDevicesAsync(new ListDevicesRequest
                    {
                        PageNum = 0, PageSize = 20, SortBy = "updateTime", SortOrder = "descending", Search = imei,
                    });
                    var d = list?.DeviceList?.FirstOrDefault(x => x.Imei == imei || x.Imei2 == imei || x.DeviceUid == imei);
                    if (d != null) found.Add(d);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Knox lookup of {Imei} failed; Knox status not saved this round.", imei);
                    return null;
                }
            }
            return found;
        }

        private async Task<List<Device>> DeviceUnlockPull(List<DeviceDto>? enrolled, IDevicesRepository deviceRepository)
        {

            var devicesToUpdate = new List<Device>();

            try
            {
                if (enrolled != null && enrolled.Any())
                {
                    foreach (var device in enrolled)
                    {
                        var deviceToDb = new Device()
                        {
                            ImeiNo = device.Imei,
                            ImeiNo2 = device.Imei2,
                            SerialNo = device.Serial,
                            IsTv = false,
                            Model = device.Model,
                            OsVersion = device.AndroidVersion,
                            SdkVersion = "", //newdevice.FirmwareVersion
                            Status = "enrolled",
                            AdminLockType = "admin_complete",
                            LockType = SetLockedByRelock(device.RelockTimestamp) == false ? "unlocked" : "complete",
                            Locked = SetLockedByRelock(device.RelockTimestamp),

                            // Knox's list gives lastSeen 0 for every phone (and our
                            // account can't read device details: 403), which used to
                            // be stored as 01-01-70. Leave the field empty then.
                            LastConnectedAt = device.LastSeen > 0
                                ? DateTimeOffset.FromUnixTimeMilliseconds(device.LastSeen).UtcDateTime.ToString("dd-MM-yy HH:mm:ss 'UTC'")
                                : null,
                            IsLockedOnSimSwap = device.IsSimControlLocked,
                            EnrollmentStatus = device.Status == "Enrolled" ? "Completed" : "Failed",
                            NextLockDateIsoFormat = TimestampHelper.FormatRelockTimestamp(device.RelockTimestamp),
                            NextLockDate = TimestampHelper.FormatDateOnly(device.RelockTimestamp)
                        };

                        var existingDevice = await deviceRepository.GetDeviceByImei(device.Imei);

                        if (existingDevice != null)
                        {
                            devicesToUpdate.Add(deviceToDb);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error while pulling devices from Knox");
            }

            if (devicesToUpdate.Any())
            {
                await deviceRepository.UpdateDevicesToDatabaseAsync(devicesToUpdate);
            }

            return devicesToUpdate;
        }

        private bool SetLockedByRelock(long? relockTimestamp)
        {
            if (!relockTimestamp.HasValue)
                return false;

            try
            {
                var relockTime = DateTimeOffset
                    .FromUnixTimeMilliseconds(relockTimestamp.Value)
                    .UtcDateTime;

                return relockTime < DateTime.UtcNow;
            }
            catch
            {
                return false;
            }
        }

    }
}
