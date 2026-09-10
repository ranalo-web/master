using Azure;
using DocumentFormat.OpenXml.Vml.Office;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Http.HttpResults;
using Ranalo.DataStore;
using Ranalo.Models;
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
        public EnrolmentService(IEnrolmentRepository enrolmentRepository,
            IVeritechApiClient veriTechClient,
            IKnoxGuardClient knoxGuardClient,
            IPayTriggerClient payTriggerClient,
            IKosePaymentsRepository kosePaymentsRepository,
            ISyncService syncService)
        {
            _enrolmentRepository = enrolmentRepository;
            _veriTechClient = veriTechClient;
            _knoxGuardClient = knoxGuardClient;
            _payTriggerClient = payTriggerClient;
            _kosePaymentsRepository = kosePaymentsRepository;
            _syncService = syncService;
        }

        // itel/TECNO/Infinix are Transsion-manufactured -- route through
        // PayTrigger instead of Knox (Samsung-only). See DeviceBrand on the
        // Enrolment model, set by the enroller via the AddEnrolment form.
        private static bool IsTranssionBrand(string? brand) =>
            brand is "itel" or "TECNO" or "Infinix";

        public async Task<Enrolment> CreateEnrolmentasync(Enrolment newEnrolment, CustomerDetails? order)
        {
            //Create Enrolment
            await _enrolmentRepository.CreateEnrolmentAsync(newEnrolment);

            return newEnrolment;
        }

        public async Task<Enrolment> StartEnrolmentasync(Enrolment newEnrolment, CustomerDetails? order)
        {
            if (IsTranssionBrand(newEnrolment.DeviceBrand))
            {
                return await StartEnrolmentasyncPayTrigger(newEnrolment, order);
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

            if(order != null)
            {
                var newContract = new ContractCreateDto() 
                { 
                    AccountNo = order.AccountId.ToString(),
                    FirstName = order.FirstName,
                    MpesaDepositRef = order.MpesaDepositRef,
                    TotalAmount = order.TotalAmount
                };

                await _syncService.CreateContractSingle(newContract);
            }
           
            return newEnrolment;
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
                    DeviceGroupId = newEnrolment.DealerId,
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

        // Transsion PayTrigger path -- mirrors StartEnrolmentasync's Knox shape
        // (background approve call -> device lookup -> write Device row), but
        // does NOT go through VeriTech first: PayTrigger is called directly.
        private async Task<Enrolment> StartEnrolmentasyncPayTrigger(Enrolment newEnrolment, CustomerDetails? order)
        {
            newEnrolment.Status = EnrolmentStatus.Pending;
            newEnrolment.Updated = DateTime.UtcNow;
            newEnrolment.UpdatedBy = "PAYTRIGGER";
            await _enrolmentRepository.UpdateEnrolmentAsync(newEnrolment);

            _ = Task.Run(async () =>
            {
                try
                {
                    // preLockFlag=false (don't lock immediately on activation)
                    // requires an initial Expiration. We don't have a real
                    // due-date yet at raw enrolment time (same as Knox), so
                    // this uses a conservative 30-day placeholder -- the
                    // first ScheduledLockPaying/Restructured run corrects it
                    // via UpdateRepayInfoAsync once real payment-cycle dates
                    // are known. TODO: revisit if PayTrigger's activation
                    // validation needs a tighter initial value.
                    var placeholderExpiration = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();

                    var enrolResponse = await _payTriggerClient.PreEnrollImeiAsync(
                        new List<PayTriggerModels.ImeiEnrollItem>
                        {
                            new()
                            {
                                Imei = newEnrolment.IMEI,
                                OrderNum = newEnrolment.OrderId.ToString(),
                                Expiration = placeholderExpiration
                            }
                        },
                        preLockFlag: false);

                    //Now update Enrolment to Approved (or Error if PayTrigger reported a failure)
                    var failed = enrolResponse.Data?.FirstOrDefault(d => d.Imei == newEnrolment.IMEI);
                    newEnrolment.Status = enrolResponse.IsSuccess && failed == null
                        ? EnrolmentStatus.Approved
                        : EnrolmentStatus.Error;
                    newEnrolment.Updated = DateTime.UtcNow;
                    newEnrolment.UpdatedBy = "PAYTRIGGER";
                    newEnrolment.PayTriggerStatus = enrolResponse.Message;
                    newEnrolment.PayTriggerResponse = failed?.Message ?? enrolResponse.Message;
                    await _enrolmentRepository.UpdateEnrolmentAsync(newEnrolment);

                    if (newEnrolment.Status == EnrolmentStatus.Approved)
                    {
                        await CreateDeviceFromPayTrigger(newEnrolment);
                    }
                }
                catch (Exception ex)
                {
                    newEnrolment.Status = EnrolmentStatus.Error;
                    newEnrolment.Updated = DateTime.UtcNow;
                    newEnrolment.UpdatedBy = "PAYTRIGGER";
                    newEnrolment.PayTriggerStatus = "Error";
                    newEnrolment.PayTriggerResponse = ex.Message;
                    await _enrolmentRepository.UpdateEnrolmentAsync(newEnrolment);
                }
            }
            );

            if (order != null)
            {
                var newContract = new ContractCreateDto()
                {
                    AccountNo = order.AccountId.ToString(),
                    FirstName = order.FirstName,
                    MpesaDepositRef = order.MpesaDepositRef,
                    TotalAmount = order.TotalAmount
                };

                await _syncService.CreateContractSingle(newContract);
            }

            return newEnrolment;
        }

        private async Task CreateDeviceFromPayTrigger(Enrolment newEnrolment)
        {
            var lockState = await DoFilterDevicesFromPayTrigger(newEnrolment.IMEI);
            var newdevice = lockState?.Data;

            if (newdevice != null)
            {
                // PayTrigger's findLockState reports MobileStatus/Expiration
                // directly (1000=locked/2000=unlock) rather than Knox's
                // relock-timestamp inference, and has no imei2/serial/
                // androidVersion/isSimControlLocked fields -- those stay
                // unset here (no equivalent data from this provider).
                var isLocked = newdevice.MobileStatus == 1000;
                var expirationMs = newdevice.Expiration.HasValue ? newdevice.Expiration.Value * 1000 : (long?)null;

                //Create a device in our db
                var deviceToDb = new Ranalo.Woocommece.Api.Models.Device()
                {
                    Id = (int)newEnrolment.AccountId,
                    Name = newEnrolment.FirstName,
                    ImeiNo = newdevice.Imei,
                    IsTv = false,
                    Model = newdevice.Model,
                    SdkVersion = "",
                    Status = "enrolled",
                    AdminLockType = "admin_complete",
                    LockType = isLocked ? "complete" : "unlocked",
                    Locked = isLocked,
                    DeviceGroupId = newEnrolment.DealerId,
                    AppVersionName = newdevice.ApkVersion,
                    CreatedAt = newdevice.ActiveTime.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(newdevice.ActiveTime.Value).UtcDateTime.ToString("dd-MM-yy HH:mm:ss 'UTC'")
                        : DateTime.UtcNow.ToString("dd-MM-yy HH:mm:ss 'UTC'"),
                    IsActivated = true,
                    LastConnectedAt = newdevice.LastConnectTime.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(newdevice.LastConnectTime.Value).UtcDateTime.ToString("dd-MM-yy HH:mm:ss 'UTC'")
                        : null,
                    EnrollmentStatus = newdevice.LockState == 3000 ? "Completed" : "Pending",
                    EnrolledOn = newEnrolment.ApprovedDate.ToString("dd-MM-yy HH:mm:ss 'UTC'"),
                    NextLockDateIsoFormat = expirationMs.HasValue ? TimestampHelper.FormatRelockTimestamp(expirationMs.Value) : null,
                    NextLockDate = expirationMs.HasValue ? TimestampHelper.FormatDateOnly(expirationMs.Value) : null,
                    LockGroup = 3 //This is PayTrigger/Transsion
                };
                // Write to DB
                await _kosePaymentsRepository.SaveDeviceToDatabaseAsync(deviceToDb);
            }
        }

        private async Task<PayTriggerModels.FindLockStateResponse> DoFilterDevicesFromPayTrigger(string imei)
        {
            //Read device details from PayTrigger
            return await _payTriggerClient.FindLockStateAsync(new PayTriggerModels.FindLockStateRequest
            {
                Imei = imei
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

        // Full-payoff device release for Transsion devices -- calls
        // PayTrigger's dedicated removeLock endpoint. PayTrigger has an
        // explicit lifecycle action for this (unlike Knox, which has no
        // equivalent wired up at all -- see ScheduledLockFullyPaid.cs's note
        // on that pre-existing gap).
        public async Task RemoveDevicesPayTrigger(List<LockTransaction> devicesToRemovePayTrigger)
        {
            foreach (var device in devicesToRemovePayTrigger)
            {
                var enrolment = await _enrolmentRepository.GetByAccountIdAsync(device.AccountId);

                await _payTriggerClient.RemoveLockAsync(new PayTriggerModels.RemoveLockRequest
                {
                    Imei = enrolment.IMEI
                });

                var existingDevice = await _kosePaymentsRepository.GetDeviceByAccountId(device.AccountId);

                if (existingDevice != null)
                {
                    existingDevice.LockType = "unlocked";
                    existingDevice.Locked = false;
                    existingDevice.Status = "removed";

                    await _kosePaymentsRepository.UpdateDeviceToDatabaseAsync(existingDevice);
                }
            }
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
