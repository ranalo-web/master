using Azure;
using DocumentFormat.OpenXml.Vml.Office;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Http.HttpResults;
using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Services.Helpers;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;
using Ranalo.VeriTechClient;
using Ranalo.Woocommece.Api.DataStore;
using System;

namespace Ranalo.Services
{
    public class EnrolmentService : IEnrolmentService
    {
        private readonly IEnrolmentRepository _enrolmentRepository;
        private readonly IVeritechApiClient _veriTechClient;
        private readonly IKnoxGuardClient _knoxGuardClient;
        private readonly IKosePaymentsRepository _kosePaymentsRepository;
        public EnrolmentService(IEnrolmentRepository enrolmentRepository, 
            IVeritechApiClient veriTechClient, 
            IKnoxGuardClient knoxGuardClient,
            IKosePaymentsRepository kosePaymentsRepository)
        {
            _enrolmentRepository = enrolmentRepository;
            _veriTechClient = veriTechClient;
            _knoxGuardClient = knoxGuardClient;
            _kosePaymentsRepository = kosePaymentsRepository;
        }

        public async Task<Enrolment> CreateEnrolmentasync(Enrolment newEnrolment, CustomerDetails? order)
        {
            //Create Enrolment
            await _enrolmentRepository.CreateEnrolmentAsync(newEnrolment);

            return newEnrolment;
        }

        public async Task<Enrolment> StartEnrolmentasync(Enrolment newEnrolment, CustomerDetails? order)
        {
            //Create Enrolment
            await _enrolmentRepository.CreateEnrolmentAsync(newEnrolment);

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

            // Call Knox and approve the device.
            var deviceToApprove = new ApproveDeviceRequest()
            {
                DeviceUid = newEnrolment.IMEI,
                ApproveId = enroll.Data.Transaction_Id, //"vkdp302411utid",
                ApproveComment = $"Approval for Order - {newEnrolment.OrderId}"
            };

            try
            {
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
            catch (Exception)
            {
                throw;
            }
            return newEnrolment;
        }

        public async Task CreateDeviceFromKnox(Enrolment newEnrolment)
        {
            //Read device details from Knox
            var deviceDetails = await _knoxGuardClient.ListDevicesAsync(new ListDevicesRequest
            {
                PageNum = 0,
                PageSize = 20,
                SortBy = "updateTime",
                SortOrder = "descending",
                Search = newEnrolment.IMEI
            });

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
        GetAllEnrolmentsAsync(int pageNumber, int pageSize)
        {
            return await _enrolmentRepository.GetAllEnrolmentsAsync(pageNumber, pageSize);
        }

        public async Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetDealerEnrolmentsAsync(int dealerId, int pageNumber, int pageSize)
        {
            return await _enrolmentRepository.GetDealerEnrolmentsAsync(dealerId, pageNumber, pageSize);
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

            try
            {
                var approvedDevice = await _knoxGuardClient.ApproveDeviceAsync(deviceToApprove);

                var responseContent = await approvedDevice.Content.ReadAsStringAsync();
                //Now update Enrolment to Approved
                existingEnrolment.Status = EnrolmentStatus.Approved;
                existingEnrolment.Updated = DateTime.UtcNow;
                existingEnrolment.UpdatedBy = "KNOX";
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

        public async Task<Enrolment> UpdateEnrolmentasync(Enrolment newEnrolment)
        {
            return await _enrolmentRepository.UpdateEnrolmentAsync(newEnrolment);
        }
    }
}
