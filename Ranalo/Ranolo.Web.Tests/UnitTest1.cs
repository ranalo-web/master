using MySqlX.XDevAPI;
using Ranalo.Services;
using Ranalo.SumsungKnox.Models;
using System.Globalization;

namespace Ranolo.Web.Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void VeriTech_Upload_Test()
        {

            //    var service = new ContractCalculatorService();
            //    var utcDate = "2025-06-04 11:43:46";

            //    DateTime tryDate = DateTime.ParseExact(utcDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            //    DateTime firstPaymentDate = DateTime.ParseExact(
            //    utcDate.Replace(" UTC", ""),          // Remove UTC for parsing
            //    "yyyy-MM-dd HH:mm:ss",                // Expected format
            //    CultureInfo.InvariantCulture,
            //    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
            //);
            //    var result = service.CalculateNoDaysUnit(firstPaymentDate);

            //    Assert.Pass();
        }

        [Test]
        public void Knox_Approval_Test()
        {
            var deviceToApprove = new ApproveDeviceRequest()
            {
                DeviceUid = "351065613616471",
                ApproveId = "vkdp302411utid",
                ApproveComment = "Test Approval comment"
            };

            //var fooBar = await _knoxSerciceClient.ApproveDeviceAsync(deviceToApprove);
        }
        [Test]
        public void Knox_Relock_Timestamp_Test()
        {
            //DateTime utcDate = DateTime.UtcNow.AddDays(1);

            //long unixTimestamp = new DateTimeOffset(utcDate)
            //    .ToUnixTimeMilliseconds();

            //var request = new DeviceActionsRequest
            //{
            //    DeviceUid = "351065613492352",
            //    ApproveId = "TestApprovalViaKnoxUI",
            //    Actions = new List<DeviceActionItem>
            //    {
            //        new DeviceActionItem
            //        {
            //            Action = "unLock",
            //            Timestamp = 0
            //        },
            //        new DeviceActionItem
            //        {
            //            Action = "lock",
            //            Timestamp = unixTimestamp,
            //            Message = "Device lock message"
            //        }
            //    }
            //};

            //var bar = await _knoxSerciceClient.ExecuteDeviceActionsAsync(request);
        }

        [Test]
        public void Knox_Unlock_Test()
        {
            //var request = new UnlockDeviceRequest
            //{
            //    DeviceUid = "453700000000106",
            //    Message = "Device unlocked after payment received"
            //};

            //await _knoxGuardClient.UnlockDeviceAsync(request);
        }

        [Test]
        public void Knox_GetDevices_List()
        {

            //var response = await _client.ListDevicesAsync(new ListDevicesRequest
            //{
            //    PageNum = 1,
            //    PageSize = 20,
            //    SortBy = "updateTime",
            //    SortOrder = "descending",
            //    Filter = new DeviceListFilter
            //    {
            //        Status = new List<string> { "ACTIVE" },
            //        SimControlEnabled = true
            //    }
            //});

        }
    }
}
