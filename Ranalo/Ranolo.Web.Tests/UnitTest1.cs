using MySqlX.XDevAPI;
using Ranalo.Calculator.Logic.Contract;
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
        // Pricing policy: WooCommerce sets the daily price; the deposit is
        // what's left of the device amount after a year of daily payments.
        [Test]
        public void Calculator_WooCommercePricing_DepositIsTotalMinusYearOfDaily()
        {
            var calculator = new ContractCalculatorService();

            Assert.That(calculator.CalculateSalesDeposit(25000m, 50m), Is.EqualTo(6750m)); // 25000 - 50*365
        }

        // Older orders with no WooCommerce daily price: 23.5% formula.
        [Test]
        public void Calculator_LegacyPricing_DepositAndDailyRate()
        {
            decimal totalAmount = 37516.48m;
            var calculator = new ContractCalculatorService();

            var deposit = calculator.CalculateDeposit(totalAmount);
            var dailyRate = calculator.CalculateDailyRate365(totalAmount, deposit);

            Assert.That(deposit, Is.EqualTo(9991.37m));   // (37516.48 + 5000) * 0.235
            Assert.That(dailyRate, Is.EqualTo(75.41m));   // (37516.48 - 9991.37) / 365
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
