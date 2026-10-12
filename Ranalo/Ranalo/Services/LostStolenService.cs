using System.Data.SqlClient;
using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services.DeviceLock;

namespace Ranalo.Services
{
    // Applies LostStolenRules to the Lost & Stolen tables.
    public interface ILostStolenService
    {
        // The reports the viewer can see: tab "open" (Pending, Confirmed) or
        // "closed" (Rejected, Closed).
        Task<LostStolenViewModel> GetPageAsync(User viewer, string? tab);

        // Search results limited to accounts the viewer may report.
        Task<List<AllAccounts>> FilterToScopeAsync(User viewer, List<AllAccounts> accounts);

        Task<(bool Ok, string Message)> ReportAsync(User reporter, NewLostStolenReport report, IReadOnlyList<LostStolenUpload> files);
        Task<(bool Ok, string Message)> AddEvidenceAsync(User uploader, int reportId, IReadOnlyList<LostStolenUpload> files);
        Task<LostStolenEvidenceFile?> GetEvidenceAsync(User viewer, int evidenceId);

        Task<(bool Ok, string Message)> ConfirmAsync(User admin, int reportId, string? note);
        Task<(bool Ok, string Message)> RejectAsync(User admin, int reportId, string? note);
        Task<(bool Ok, string Message)> ResendLockAsync(User admin, int reportId);
        Task<(bool Ok, string Message)> CloseAsync(User admin, int reportId, string? outcome, string? note);
    }

    public class LostStolenService : ILostStolenService
    {
        private readonly ILostStolenRepository _repository;
        private readonly ILostStolenLocker _locker;
        private readonly TimeProvider _time;
        private readonly ILogger<LostStolenService> _logger;

        public LostStolenService(ILostStolenRepository repository, ILostStolenLocker locker, TimeProvider time, ILogger<LostStolenService> logger)
        {
            _repository = repository;
            _locker = locker;
            _time = time;
            _logger = logger;
        }

        private DateTime Today => _time.GetLocalNow().Date;

        public async Task<LostStolenViewModel> GetPageAsync(User viewer, string? tab)
        {
            var model = new LostStolenViewModel
            {
                Tab = tab == "closed" ? "closed" : "open",
                CanDecide = LostStolenRules.CanDecide(viewer.RoleId),
            };

            try
            {
                var reports = await _repository.GetReportsAsync(open: model.Tab == "open");
                model.Reports = await VisibleAsync(viewer, reports);
                if (model.Tab == "open")
                {
                    model.PendingCount = model.Reports.Count(r => r.Status == LostStolenStatus.Pending);
                    model.ConfirmedCount = model.Reports.Count(r => r.Status == LostStolenStatus.Confirmed);
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                _logger.LogWarning(ex, "Lost & Stolen tables missing -- run Database/LostStolen/001_create_lost_stolen_reports.sql");
                model.SetupMissing = true;
            }
            return model;
        }

        public async Task<List<AllAccounts>> FilterToScopeAsync(User viewer, List<AllAccounts> accounts)
        {
            if (viewer.RoleId is UserRole.Admin or UserRole.Approver) return accounts;
            var scopes = await _repository.GetScopesAsync(accounts.Select(a => (long)a.AccountNo).ToList());
            return accounts.Where(a => scopes.TryGetValue(a.AccountNo, out var s) && LostStolenRules.InScope(viewer, s)).ToList();
        }

        public async Task<(bool Ok, string Message)> ReportAsync(User reporter, NewLostStolenReport report, IReadOnlyList<LostStolenUpload> files)
        {
            if (!LostStolenRules.CanUsePage(reporter)) return (false, "You can't report lost or stolen phones.");

            var invalid = LostStolenRules.ValidateReport(report, Today);
            if (invalid != null) return (false, invalid);

            var (checkedFiles, fileError) = CheckFiles(files);
            if (fileError != null) return (false, fileError);

            if (!await InScopeAsync(reporter, report.AccountNo))
            {
                return (false, $"Account {report.AccountNo} isn't one of your accounts.");
            }

            var reportId = await _repository.CreateAsync(report, reporter.UserId, reporter.RoleId.ToString());
            if (reportId == null)
            {
                return (false, $"Account {report.AccountNo} already has an open lost/stolen report. Add evidence to that one.");
            }

            foreach (var f in checkedFiles)
            {
                await _repository.AddEvidenceAsync(reportId.Value, f.FileName, f.ContentType, f.Content, reporter.UserId);
            }

            return (true, checkedFiles.Count == 0
                ? $"Reported. Upload the police abstract or other evidence so an admin can confirm it and lock the phone."
                : $"Reported with {checkedFiles.Count} file(s). An admin will review it and lock the phone.");
        }

        public async Task<(bool Ok, string Message)> AddEvidenceAsync(User uploader, int reportId, IReadOnlyList<LostStolenUpload> files)
        {
            if (files.Count == 0) return (false, "Choose a file to upload.");

            var (checkedFiles, fileError) = CheckFiles(files);
            if (fileError != null) return (false, fileError);

            var report = await _repository.GetReportAsync(reportId);
            if (report == null || !await InScopeAsync(uploader, report.AccountNo)) return (false, "Report not found.");
            if (!report.IsOpen) return (false, "This report is closed.");

            foreach (var f in checkedFiles)
            {
                await _repository.AddEvidenceAsync(reportId, f.FileName, f.ContentType, f.Content, uploader.UserId);
            }
            return (true, $"{checkedFiles.Count} file(s) added to the report for account {report.AccountNo}.");
        }

        public async Task<LostStolenEvidenceFile?> GetEvidenceAsync(User viewer, int evidenceId)
        {
            var file = await _repository.GetEvidenceFileAsync(evidenceId);
            if (file == null) return null;
            var report = await _repository.GetReportAsync(file.ReportId);
            return report != null && await InScopeAsync(viewer, report.AccountNo) ? file : null;
        }

        public async Task<(bool Ok, string Message)> ConfirmAsync(User admin, int reportId, string? note)
        {
            if (!LostStolenRules.CanDecide(admin.RoleId)) return (false, "Only an admin can confirm a report.");

            var report = await _repository.GetReportAsync(reportId);
            if (report == null) return (false, "Report not found.");

            var problem = LostStolenRules.CheckConfirm(report);
            if (problem != null) return (false, problem);

            if (!await _repository.ConfirmAsync(reportId, admin.UserId, CollectionsRules.CleanNote(note)))
            {
                return (false, "Someone else has already decided this report.");
            }

            return await LockAsync(report, "Confirmed");
        }

        public async Task<(bool Ok, string Message)> RejectAsync(User admin, int reportId, string? note)
        {
            if (!LostStolenRules.CanDecide(admin.RoleId)) return (false, "Only an admin can reject a report.");

            var clean = CollectionsRules.CleanNote(note);
            if (clean == null) return (false, "Say why it's rejected -- the reporter will see this.");

            return await _repository.RejectAsync(reportId, admin.UserId, clean)
                ? (true, "Report rejected. The phone was not locked.")
                : (false, "This report has already been decided.");
        }

        public async Task<(bool Ok, string Message)> ResendLockAsync(User admin, int reportId)
        {
            if (!LostStolenRules.CanDecide(admin.RoleId)) return (false, "Only an admin can lock a phone.");

            var report = await _repository.GetReportAsync(reportId);
            if (report == null) return (false, "Report not found.");
            if (report.Status != LostStolenStatus.Confirmed) return (false, "Only a confirmed report's phone can be locked.");

            return await LockAsync(report, "Lock sent again");
        }

        public async Task<(bool Ok, string Message)> CloseAsync(User admin, int reportId, string? outcome, string? note)
        {
            if (!LostStolenRules.CanDecide(admin.RoleId)) return (false, "Only an admin can close a report.");
            if (!LostStolenRules.IsOutcome(outcome)) return (false, "Choose how it ended: found, paid off or written off.");

            return await _repository.CloseAsync(reportId, outcome!, admin.UserId, CollectionsRules.CleanNote(note))
                ? (true, outcome == LostStolenOutcome.Found
                    ? "Closed as found. The nightly jobs manage the phone's lock again from their next run."
                    : $"Closed as {LostStolenOutcome.Label(outcome).ToLowerInvariant()}.")
                : (false, "Only a confirmed report can be closed.");
        }

        private async Task<(bool Ok, string Message)> LockAsync(LostStolenReport report, string done)
        {
            var scope = (await _repository.GetScopesAsync(new[] { report.AccountNo })).GetValueOrDefault(report.AccountNo);
            var (sent, result) = await _locker.LockNowAsync(report.AccountNo, scope?.LockGroup);
            await _repository.RecordLockAsync(report.Id, (sent ? "Sent. " : "FAILED. ") + result);

            return sent
                ? (true, $"{done}. Lock sent to account {report.AccountNo}'s phone ({result}).")
                : (false, $"{done}, but the lock didn't go through: {result}. Use \"Lock again\" to retry.");
        }

        private async Task<bool> InScopeAsync(User user, long accountNo)
        {
            var scope = (await _repository.GetScopesAsync(new[] { accountNo })).GetValueOrDefault(accountNo);
            return scope != null && LostStolenRules.InScope(user, scope);
        }

        private async Task<List<LostStolenReport>> VisibleAsync(User viewer, List<LostStolenReport> reports)
        {
            if (viewer.RoleId is UserRole.Admin or UserRole.Approver || reports.Count == 0) return reports;
            var scopes = await _repository.GetScopesAsync(reports.Select(r => r.AccountNo).ToList());
            return reports.Where(r => scopes.TryGetValue(r.AccountNo, out var s) && LostStolenRules.InScope(viewer, s)).ToList();
        }

        private static (List<(string FileName, string ContentType, byte[] Content)> Files, string? Error) CheckFiles(IReadOnlyList<LostStolenUpload> files)
        {
            var result = new List<(string, string, byte[])>();
            if (files.Count > LostStolenRules.MaxFilesPerUpload)
            {
                return (result, $"Upload at most {LostStolenRules.MaxFilesPerUpload} files at a time.");
            }

            foreach (var f in files)
            {
                var name = LostStolenRules.SafeFileName(f.FileName);
                var (type, error) = LostStolenRules.CheckEvidence(name, f.Content);
                if (error != null) return (result, error);
                result.Add((name, type!, f.Content));
            }
            return (result, null);
        }
    }
}
