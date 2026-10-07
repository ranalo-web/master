using Azure;
using DocumentFormat.OpenXml.Vml.Office;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Http.HttpResults;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services.DeviceLock;
using Ranalo.Services.Helpers;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.PayTrigger;
using PayTriggerModels = Ranalo.PayTrigger.Models;
using Ranalo.VeriTechClient;
using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;
using System;

namespace Ranalo.Services
{
    public class EnrolmentService : IEnrolmentService
    {
        private readonly IEnrolmentRepository _enrolmentRepository;
        private readonly IVeritechApiClient _veriTechClient;
        private readonly IKnoxGuardClient _knoxGuardClient;
        private readonly IPayTriggerClient _payTriggerClient;
        private readonly IKosePaymentsRepository _kosePaymentsRepository;
        private readonly ISyncService _syncService;
        private readonly IRepository _dealers;
        private readonly ITranssionEnrolmentWorkflow _transsionWorkflow;
        public EnrolmentService(IEnrolmentRepository enrolmentRepository,
            IVeritechApiClient veriTechClient,
            IKnoxGuardClient knoxGuardClient,
            IPayTriggerClient payTriggerClient,
            IKosePaymentsRepository kosePaymentsRepository,
            ISyncService syncService,
            IRepository dealers,
            ITranssionEnrolmentWorkflow transsionWorkflow)
        {
            _dealers = dealers;
            _transsionWorkflow = transsionWorkflow;
            _enrolmentRepository = enrolmentRepository;
            _veriTechClient = veriTechClient;
            _knoxGuardClient = knoxGuardClient;
            _payTriggerClient = payTriggerClient;
            _kosePaymentsRepository = kosePaymentsRepository;
            _syncService = syncService;
        }

        public async Task<Enrolment> CreateEnrolmentasync(Enrolment newEnrolment, CustomerDetails? order)
        {
            //Create Enrolment
            await _enrolmentRepository.CreateEnrolmentAsync(newEnrolment);

            return newEnrolment;
        }

        public async Task<Enrolment> StartEnrolmentasync(Enrolment newEnrolment, CustomerDetails? order)
        {
            if (DeviceLockRules.IsTranssion(newEnrolment.DeviceBrand))
            {
                return await _transsionWorkflow.PreEnrolAsync(
                    newEnrolment, await DeviceGroupForDealerAsync(newEnrolment.DealerId));
            }

            //Create Enrolment
            //await _enrolmentRepository.CreateEnrolmentAsync(newEnrolment);

            //Need to Call Veritech to enrol a device
            var deviceToEnrol = new List<string>() { newEnrolment.IMEI };
            var enroll = await _veriTechClient.UploadDevicesAsync(deviceToEnrol);

            //Update the enrolment status
            newEnrolment.Status = EnrolmentStatus.Pending;
            newEnrolment.Updated = DateTime.UtcNow;
            newEnrolment.UpdatedBy = "VERITECH";
            newEnrolment.VeriTechCode = enroll.Data.Code;
            newEnrolment.VeriTechData = enroll.Data.Data;
            newEnrolment.VeriTechTransId = enroll.Data.Transaction_Id;
            newEnrolment.VeriTechStatus = enroll.Data.Status;
            newEnrolment.VeriTechMessage = enroll.Data.Message;

            await _enrolmentRepository.UpdateEnrolmentAsync(newEnrolment);

            _ = Task.Run(async () =>
            {
                try
                {
                    // Call Knox and approve the device.
                    var deviceToApprove = new ApproveDeviceRequest()
                    {
                        DeviceUid = newEnrolment.IMEI,
                        ApproveId = enroll.Data.Transaction_Id, //"vkdp302411utid",
                        ApproveComment = $"Approval for Order - {newEnrolment.OrderId}"
                    };

                    var approvedDevice = await _knoxGuardClient.ApproveDeviceAsync(deviceToApprove);

                    var responseContent = await approvedDevice.Content.ReadAsStringAsync();
                    //Now update Enrolment to Approved
                    newEnrolment.Status = EnrolmentStatus.Approved;
                    newEnrolment.Updated = DateTime.UtcNow;
                    newEnrolment.UpdatedBy = "KNOX";
                    newEnrolment.KnoxResponse = responseContent;
                    await _enrolmentRepository.UpdateEnrolmentAsync(newEnrolment);

                    await CreateDeviceFromKnox(newEnrolment);

                }
                catch (Exception ex)
                {
                    newEnrolment.Status = EnrolmentStatus.Error;
                    newEnrolment.Updated = DateTime.UtcNow;
                    newEnrolment.UpdatedBy = "KNOX";
                    newEnrolment.KnoxResponse = ex.Message;
                    await _enrolmentRepository.UpdateEnrolmentAsync(newEnrolment);
                }
            }
            );

            // The contract is created by ScheduledTaskCreateContractOrders,
            // from the WooCommerce order (its pricing) and the account the
            // deposit was paid to. Creating it here used order.AccountId,
            // which the order lookup never fills, so it made contracts on
            // account 0 with the old deposit formula.
            return newEnrolment;
        }

        // A device's group must be its dealer's DealerReference: every page
        // finds an account's dealer via Devices.DeviceGroupId =
        // Dealers.DealerReference. Enrolment.DealerId is the Dealers.DealerId
        // key, which only matched by accident (admin enrolments, DealerId 0 =
        // Renalo's reference "0000"). No dealer found: keep the id as before.
        private async Task<int?> DeviceGroupForDealerAsync(int dealerId)
        {
            var dealer = await _dealers.GetDealerByDealerIdAsync(dealerId);
            return dealer != null && int.TryParse(dealer.DealerReference, out var group) ? group : dealerId;
        }

        public async Task CreateDeviceFromKnox(Enrolment newEnrolment)
        {
            ListDevicesResponse deviceDetails = await DoFilterDevicesFromKnox(newEnrolment.IMEI);

            if (deviceDetails != null && deviceDetails.DeviceList != null && deviceDetails.DeviceList.Any())
            {
                var newdevice = deviceDetails.DeviceList.FirstOrDefault(x => x.Imei == newEnrolment.IMEI);
                //Create a device in our db
                var deviceToDb = new Ranalo.Woocommece.Api.Models.Device()
                {
                    Id = (int)newEnrolment.AccountId,
                    Name = newEnrolment.FirstName,
                    ImeiNo = newdevice.Imei,
                    ImeiNo2 = newdevice.Imei2,
                    SerialNo = newdevice.Serial,
                    IsTv = false,
                    Model = newdevice.Model,
                    OsVersion = newdevice.AndroidVersion,
                    SdkVersion = "", //newdevice.FirmwareVersion
                    Status = "enrolled",
                    AdminLockType = "admin_complete",
                    LockType = SetLockedByRelock(newdevice.RelockTimestamp) == false ? "unlocked" : "complete",
                    Locked = SetLockedByRelock(newdevice.RelockTimestamp),
                    DeviceGroupId = await DeviceGroupForDealerAsync(newEnrolment.DealerId),
                    // AppVersionCode = newdevice.AgentVersion,
                    AppVersionName = newdevice.FirmwareVersion,
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(newdevice.CreateDate).UtcDateTime.ToString("dd-MM-yy HH:mm:ss 'UTC'"),
                    IsActivated = true,
                    LastConnectedAt = DateTimeOffset.FromUnixTimeMilliseconds(newdevice.LastSeen).UtcDateTime.ToString("dd-MM-yy HH:mm:ss 'UTC'"),
                    IsLockedOnSimSwap = newdevice.IsSimControlLocked,
                    EnrollmentStatus = newdevice.Status == "Enrolled" ? "Completed" : "Failed",
                    EnrolledOn = newEnrolment.ApprovedDate.ToString("dd-MM-yy HH:mm:ss 'UTC'"),
                    NextLockDateIsoFormat = TimestampHelper.FormatRelockTimestamp(newdevice.RelockTimestamp),
                    NextLockDate = TimestampHelper.FormatDateOnly(newdevice.RelockTimestamp),
                    LockGroup = 2 //This is knox
                };
                // Write to DB
                await _kosePaymentsRepository.SaveDeviceToDatabaseAsync(deviceToDb);

            }
        }

        public async Task<ListDevicesResponse> DoFilterDevicesFromKnox(string imei)
        {
            //Read device details from Knox
            return await _knoxGuardClient.ListDevicesAsync(new ListDevicesRequest
            {
                PageNum = 0,
                PageSize = 20,
                SortBy = "updateTime",
                SortOrder = "descending",
                Search = imei
            });
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

        public async Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetAllEnrolmentsAsync(int pageNumber, int pageSize, string? searchTerm = null)
        {
            return await _enrolmentRepository.GetAllEnrolmentsAsync(pageNumber, pageSize, searchTerm);
        }

        public async Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetDealerEnrolmentsAsync(int dealerId, int pageNumber, int pageSize, string? searchTerm = null)
        {
            return await _enrolmentRepository.GetDealerEnrolmentsAsync(dealerId, pageNumber, pageSize, searchTerm);
        }

        public async Task<Enrolment?> GetByImeiNumberAsync(string imei)
        {
            return await _enrolmentRepository.GetByImeiNumberAsync(imei);
        }

        public async Task ApproveEnrolment(Enrolment existingEnrolment)
        {
            // Call Knox and approve the device.
            var deviceToApprove = new ApproveDeviceRequest()
            {
                DeviceUid = existingEnrolment.IMEI,
                ApproveId = existingEnrolment.VeriTechTransId, //"vkdp302411utid",
                ApproveComment = $"Approval for Order - {existingEnrolment.OrderId}"
            };

           // var foo = await _veriTechClient.GetDevicesAsync();

            try
            {
                var approvedDevice = await _knoxGuardClient.ApproveDeviceAsync(deviceToApprove);

                var responseContent = await approvedDevice.Content.ReadAsStringAsync();
                //Now update Enrolment to Approved
                existingEnrolment.Status = EnrolmentStatus.Approved;
                existingEnrolment.Updated = DateTime.UtcNow;
                existingEnrolment.UpdatedBy = "KNOX";
                existingEnrolment.ApprovedDate = DateTime.UtcNow;
                existingEnrolment.KnoxResponse = responseContent;
                await _enrolmentRepository.UpdateEnrolmentAsync(existingEnrolment);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<Enrolment> GetByEnrolmentIdNumberAsync(Guid enrolmentId)
        {
            return await _enrolmentRepository.GetByEnrolmentIdAsync(enrolmentId);
        }

        public async Task DeleteNewEnrolmentEnrolment(Enrolment existingEnrolment)
        {
            await _enrolmentRepository.DeleteEnrolmentAsync(existingEnrolment);
        }

        public async Task LockDevicesKnox(List<LockTransaction> devicesToLockKnox)
        {

            foreach (var device in devicesToLockKnox)
            {
                // Get the enrolment our link to the record 
                var enrolment = await _enrolmentRepository.GetByAccountIdAsync(device.AccountId);

                long unixTimestamp = new DateTimeOffset(device.AutoLockDate)
                    .ToUnixTimeMilliseconds();

                var request = new DeviceActionsRequest
                {
                    DeviceUid = enrolment.IMEI,  //"351065613492352",
                    ApproveId = enrolment.VeriTechTransId, // "TestApprovalViaKnoxUI",
                    Actions = new List<DeviceActionItem>
                {
                    new DeviceActionItem
                    {
                        Action = "unLock",
                        Timestamp = 0
                    },
                    new DeviceActionItem
                    {
                        Action = "lock",
                        Timestamp = unixTimestamp,
                        Message = "Device lock message"
                    }
                }
                };

                await _knoxGuardClient.ExecuteDeviceActionsAsync(request);

                //TODO:If this succeds we need to update the devices table
                var existingDevice = await _kosePaymentsRepository.GetDeviceByAccountId(device.AccountId);

                if(existingDevice != null)
                {
                    existingDevice.LockType = SetLockedByRelock(unixTimestamp) == false ? "unlocked" : "complete";
                    existingDevice.Locked = SetLockedByRelock(unixTimestamp);
                    existingDevice.NextLockDate = TimestampHelper.FormatDateOnly(unixTimestamp);
                    existingDevice.NextLockDateIsoFormat = TimestampHelper.FormatRelockTimestamp(unixTimestamp);

                    await _kosePaymentsRepository.UpdateDeviceToDatabaseAsync(existingDevice);
                }
            }

        }

        // Payment-driven lock-date extension for Transsion devices -- calls
        // PayTrigger's real updateRepayInfo endpoint (unlocks the device and
        // pushes the new due/lock date), unlike Knox's ExecuteDeviceActionsAsync
        // unlock-then-lock pair. Called by ScheduledLockPaying/Restructured/
        // AutoRestructured for LockGroup==3 devices.
        public async Task LockDevicesPayTrigger(List<LockTransaction> devicesToLockPayTrigger)
        {
            foreach (var device in devicesToLockPayTrigger)
            {
                // Get the enrolment our link to the record
                var enrolment = await _enrolmentRepository.GetByAccountIdAsync(device.AccountId);

                long unixSeconds = new DateTimeOffset(device.AutoLockDate).ToUnixTimeSeconds();
                long unixMillis = unixSeconds * 1000;

                var request = new PayTriggerModels.UpdateRepayInfoRequest
                {
                    Imei = enrolment.IMEI,
                    NextRepayTime = unixSeconds
                };

                await _payTriggerClient.UpdateRepayInfoAsync(request);

                var existingDevice = await _kosePaymentsRepository.GetDeviceByAccountId(device.AccountId);

                if (existingDevice != null)
                {
                    existingDevice.LockType = "unlocked";
                    existingDevice.Locked = false;
                    existingDevice.NextLockDate = TimestampHelper.FormatDateOnly(unixMillis);
                    existingDevice.NextLockDateIsoFormat = TimestampHelper.FormatRelockTimestamp(unixMillis);

                    await _kosePaymentsRepository.UpdateDeviceToDatabaseAsync(existingDevice);
                }
            }
        }

        // Fully paid, Knox: unlock with no relock scheduled, and record the
        // 31/12/9999 lock date. A lock action dated 9999 isn't sent -- if Knox
        // rejected it the unlock would fail with it. Our row is only updated
        // when Knox accepts, so a failure is picked up again next run.
        public async Task<List<LockTransaction>> SetFullyPaidKnox(List<LockTransaction> devices)
        {
            foreach (var device in devices)
            {
                try
                {
                    var enrolment = await _enrolmentRepository.GetByAccountIdAsync(device.AccountId);
                    var existingDevice = await _kosePaymentsRepository.GetDeviceByAccountId(device.AccountId);
                    var imei = existingDevice?.ImeiNo ?? enrolment?.IMEI;
                    if (string.IsNullOrEmpty(imei))
                    {
                        device.Result = "No IMEI found";
                        continue;
                    }

                    var response = await _knoxGuardClient.ExecuteDeviceActionsAsync(new DeviceActionsRequest
                    {
                        DeviceUid = imei,
                        ApproveId = enrolment?.VeriTechTransId,
                        Actions = new List<DeviceActionItem> { new DeviceActionItem { Action = "unLock", Timestamp = 0 } }
                    });

                    device.Result = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
                    if (response.IsSuccessStatusCode && existingDevice != null)
                    {
                        SetFullyPaidLockDate(existingDevice);
                        await _kosePaymentsRepository.UpdateDeviceToDatabaseAsync(existingDevice);
                    }
                }
                catch (Exception ex)
                {
                    device.Result = ex.Message;
                }
            }

            return devices;
        }

        // Fully paid, Transsion: push the 31/12/9999 lock date, or 31/12/2099
        // if Transsion refuses 9999. Our row records whichever was accepted,
        // and is only updated when one is.
        public async Task<List<LockTransaction>> SetFullyPaidPayTrigger(List<LockTransaction> devices)
        {
            foreach (var device in devices)
            {
                try
                {
                    var existingDevice = await _kosePaymentsRepository.GetDeviceByAccountId(device.AccountId);
                    var imei = existingDevice?.ImeiNo
                               ?? (await _enrolmentRepository.GetByAccountIdAsync(device.AccountId))?.IMEI;
                    if (string.IsNullOrEmpty(imei))
                    {
                        device.Result = "No IMEI found";
                        continue;
                    }

                    var results = new List<string>();
                    foreach (var lockDate in new[] { DeviceLockRules.FullyPaidLockDateUtc, DeviceLockRules.FullyPaidFallbackLockDateUtc })
                    {
                        var response = await _payTriggerClient.UpdateRepayInfoAsync(new PayTrigger.Models.UpdateRepayInfoRequest
                        {
                            Imei = imei,
                            NextRepayTime = new DateTimeOffset(lockDate).ToUnixTimeSeconds(),
                            Description = "Fully paid"
                        });

                        results.Add($"{lockDate.Year}: {response.Code} {response.Message}");
                        if (!response.IsSuccess)
                        {
                            continue;
                        }

                        device.AutoLockDate = lockDate;
                        if (existingDevice != null)
                        {
                            SetFullyPaidLockDate(existingDevice, lockDate);
                            await _kosePaymentsRepository.UpdateDeviceToDatabaseAsync(existingDevice);
                        }
                        break;
                    }

                    device.Result = string.Join("; ", results);
                }
                catch (Exception ex)
                {
                    device.Result = ex.Message;
                }
            }

            return devices;
        }

        private static void SetFullyPaidLockDate(Ranalo.Woocommece.Api.Models.Device device, DateTime? lockDateUtc = null)
        {
            var millis = new DateTimeOffset(lockDateUtc ?? DeviceLockRules.FullyPaidLockDateUtc).ToUnixTimeMilliseconds();
            device.Locked = false;
            device.LockType = "unlocked";
            device.NextLockDate = TimestampHelper.FormatDateOnly(millis);
            device.NextLockDateIsoFormat = TimestampHelper.FormatRelockTimestamp(millis);
        }

        public async Task<Enrolment> UpdateEnrolmentasync(Enrolment newEnrolment)
        {
            return await _enrolmentRepository.UpdateEnrolmentAsync(newEnrolment);
        }

        public async Task SendReminderMessage(string imei)
        {
            var message = new SendMessageRequest() 
            { 
                DeviceUid = imei,
                Message = "Test Knox Message",
                //ObjectId = "TestObj",
                //ApproveId = "24063589",
                Tel = "0001112233444"

            };
            var foo = await _knoxGuardClient.SendMessageAsync(message);


        }
        }
    }
