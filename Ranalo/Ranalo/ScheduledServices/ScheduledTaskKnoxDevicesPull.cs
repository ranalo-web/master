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
                        var inactiveUsers = await DeviceUnlockPull(knoxClient, deviceRepository);
                        foreach (var device in inactiveUsers)
                        {
                            _logger.LogInformation("Device Updated from Knox: {order}", device.Id);
                            // Possibly send email reminders, clean up data, etc.
                        }
                    }

                    // Wait until next run
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Ignore when shutting down
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while running scheduled ScheduledTaskKnoxDevicesPull.");
                }
            }

            _logger.LogInformation("ScheduledTaskKnoxDevicesPull stopped at: {time}", DateTime.UtcNow);
        }

        private async Task<List<Device>> DeviceUnlockPull(IKnoxGuardClient knoxClient, IDevicesRepository deviceRepository)
        {

            var devicesToUpdate = new List<Device>();

            try
            {
                var devices = await knoxClient.ListDevicesAsync(new ListDevicesRequest
                {
                    Filter = new DeviceListFilter() { Status = new List<string>() { "Enrolled" } },
                    PageNum = 0,
                    PageSize = 20,
                    SortBy = "updateTime",
                    SortOrder = "descending"
                });

                if (devices != null && devices.DeviceList != null && devices.DeviceList.Any())
                {


                    foreach (var device in devices.DeviceList)
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

                            LastConnectedAt = DateTimeOffset.FromUnixTimeMilliseconds(device.LastSeen).UtcDateTime.ToString("dd-MM-yy HH:mm:ss 'UTC'"),
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
