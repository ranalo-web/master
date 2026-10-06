using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services.DeviceLock;

namespace Ranalo.Controllers
{
    // Admin-only queue of fully-paid devices waiting to be removed from their
    // lock provider. See DeviceRemovalService and Database/DeviceLock/001.
    [LoadUserSettingsFromCookie]
    public class PendingTasksController : Controller
    {
        // Each provider call is a few seconds (Knox waits 3s per call), so a
        // batch is capped to stay well inside the 230s Azure request timeout.
        public const int MaxApprovalsPerBatch = 25;

        private readonly IDeviceRemovalTaskRepository _tasks;
        private readonly IDeviceRemovalService _removals;

        public PendingTasksController(IDeviceRemovalTaskRepository tasks, IDeviceRemovalService removals)
        {
            _tasks = tasks;
            _removals = removals;
        }

        [HttpGet]
        [Route("pending-tasks")]
        public async Task<IActionResult> Index(string status = DeviceRemovalStatus.Awaiting, int page = 1, int pageSize = 50)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!DeviceLockRules.CanDecideRemovals(settings.RoleId))
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "pending-tasks";
            ViewBag.IsAdmin = true;
            ViewBag.UserName = settings.KnownAs;

            var allowed = new[]
            {
                DeviceRemovalStatus.Awaiting, DeviceRemovalStatus.Pending, DeviceRemovalStatus.Failed,
                DeviceRemovalStatus.Processing, DeviceRemovalStatus.Completed, DeviceRemovalStatus.Rejected,
                DeviceRemovalStatus.All
            };
            if (!allowed.Contains(status))
            {
                status = DeviceRemovalStatus.Awaiting;
            }

            var (items, total) = await _tasks.ListAsync(status, page, pageSize);
            var model = new PendingTasksViewModel
            {
                Tasks = items,
                Status = status,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = total
            };

            if (TempData["PendingTasksMessages"] is string messages)
            {
                model.Messages.AddRange(messages.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
            if (TempData["PendingTasksErrors"] is string errors)
            {
                model.Errors.AddRange(errors.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("pending-tasks/approve")]
        public async Task<IActionResult> Approve(List<int> taskIds)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!DeviceLockRules.CanDecideRemovals(settings.RoleId))
            {
                return RedirectToAction("Index", "Home");
            }

            if (taskIds == null || taskIds.Count == 0)
            {
                TempData["PendingTasksErrors"] = "Tick at least one device.";
                return RedirectToAction("Index");
            }

            var messages = new List<string>();
            var errors = new List<string>();
            var ids = taskIds.Distinct().ToList();
            if (ids.Count > MaxApprovalsPerBatch)
            {
                errors.Add($"Only the first {MaxApprovalsPerBatch} ticked devices were processed. Approve the rest in another batch.");
                ids = ids.Take(MaxApprovalsPerBatch).ToList();
            }

            foreach (var id in ids)
            {
                var result = await _removals.ApproveAsync(id, settings);
                (result.Success ? messages : errors).Add(result.Message);
            }

            TempData["PendingTasksMessages"] = string.Join('\n', messages);
            TempData["PendingTasksErrors"] = string.Join('\n', errors);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("pending-tasks/reject")]
        public async Task<IActionResult> Reject(int taskId, string? note)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!DeviceLockRules.CanDecideRemovals(settings.RoleId))
            {
                return RedirectToAction("Index", "Home");
            }

            var result = await _removals.RejectAsync(taskId, settings, note);
            TempData[result.Success ? "PendingTasksMessages" : "PendingTasksErrors"] = result.Message;
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("pending-tasks/scan")]
        public async Task<IActionResult> Scan()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (!DeviceLockRules.CanDecideRemovals(settings.RoleId))
            {
                return RedirectToAction("Index", "Home");
            }

            var queued = await _removals.QueueExistingFullyPaidAsync();
            TempData["PendingTasksMessages"] = queued == 0
                ? "No other fully-paid devices are waiting to be queued."
                : $"Queued {queued} fully-paid device(s) already at the fully-paid lock date.";
            return RedirectToAction("Index");
        }
    }
}
