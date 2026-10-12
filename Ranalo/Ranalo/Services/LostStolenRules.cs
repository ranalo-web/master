using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services
{
    // Lost and stolen phones (agreed with the business 2026-10-12):
    //   * The customer is still liable: nothing here pauses the contract.
    //   * Admin, Approver, Dealer, Agent and Collector can report a phone on an
    //     account they can see, with the police abstract or other evidence.
    //   * Only an admin confirms (the phone is locked at once) or rejects, and
    //     closes the report: Found, Paid off or Written off.
    //   * While Confirmed the nightly lock jobs leave the phone alone, so a
    //     customer's payment doesn't unlock it for whoever has it.
    public static class LostStolenRules
    {
        // 0.25 MB each for now (agreed 2026-10-12): enough for a phone photo
        // of the abstract at normal quality, and keeps the database small.
        public const int MaxFileBytes = 256 * 1024;
        public const string MaxFileSizeLabel = "0.25 MB";
        public const int MaxFilesPerUpload = 5;
        public const int DescriptionMaxLength = 1000;
        public const int PoliceRefMaxLength = 100;
        public const int NoteMaxLength = 500;

        public static bool CanUsePage(User user) =>
            user.RoleId is UserRole.Admin or UserRole.Approver or UserRole.Dealer or UserRole.Agent
            || CollectionsService.IsCollector(user);

        public static bool CanDecide(UserRole role) => role == UserRole.Admin;

        // Admin and Approver see every account; a dealer their dealer's; an
        // agent the accounts assigned to them; a collector the accounts they
        // hold in collections (a dealer or agent who is also a collector gets
        // both).
        public static bool InScope(User user, LostStolenAccountScope account)
        {
            if (!account.HasDevice) return false;   // no phone on the account to lose
            if (user.RoleId is UserRole.Admin or UserRole.Approver) return true;
            if (user.RoleId == UserRole.Dealer && account.DealerId.HasValue && account.DealerId == user.DealerId) return true;
            if (user.RoleId == UserRole.Agent && account.AssignedAgentId == user.UserId) return true;
            return CollectionsService.IsCollector(user) && account.CollectorUserId == user.UserId;
        }

        // Null when the report is fine, else what to fix.
        public static string? ValidateReport(NewLostStolenReport r, DateTime today)
        {
            if (r.Kind is not (LostStolenKind.Lost or LostStolenKind.Stolen)) return "Choose lost or stolen.";
            if (r.IncidentDate == null) return "Enter the date the phone was lost or stolen.";
            if (r.IncidentDate.Value.Date > today.Date) return "The date can't be in the future.";
            if (string.IsNullOrWhiteSpace(r.Description)) return "Say what happened.";
            if (r.Description.Trim().Length > DescriptionMaxLength) return $"Keep what happened under {DescriptionMaxLength} characters.";
            if (r.PoliceRef?.Trim().Length > PoliceRefMaxLength) return $"The police reference is too long (max {PoliceRefMaxLength}).";
            return null;
        }

        // Images and PDFs only, checked by extension AND by the file's first
        // bytes (a renamed .exe isn't a .pdf). Returns the content type to
        // store, or null with the reason.
        public static (string? ContentType, string? Error) CheckEvidence(string fileName, byte[] content)
        {
            if (content.Length == 0) return (null, $"{fileName} is empty.");
            if (content.Length > MaxFileBytes) return (null, $"{fileName} is over {MaxFileSizeLabel}. Take the photo at a lower size, or scan it smaller.");

            var type = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                _ => null,
            };
            if (type == null) return (null, $"{fileName}: upload a photo (JPG, PNG, WEBP) or a PDF.");

            var matches = type switch
            {
                "image/jpeg" => StartsWith(content, 0xFF, 0xD8, 0xFF),
                "image/png" => StartsWith(content, 0x89, 0x50, 0x4E, 0x47),
                "application/pdf" => StartsWith(content, 0x25, 0x50, 0x44, 0x46),   // %PDF
                "image/webp" => content.Length >= 12 && StartsWith(content, 0x52, 0x49, 0x46, 0x46)  // RIFF....WEBP
                                && content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50,
                _ => false,
            };
            return matches ? (type, null) : (null, $"{fileName} isn't a real {Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant()} file.");
        }

        // An admin confirms only with evidence to look at.
        public static string? CheckConfirm(LostStolenReport report)
        {
            if (report.Status != LostStolenStatus.Pending) return $"This report is already {report.Status.ToLowerInvariant()}.";
            if (report.Evidence.Count == 0) return "Upload the police abstract or other evidence before confirming.";
            return null;
        }

        // The nightly lock jobs drop accounts whose phone is confirmed lost or
        // stolen, so a payment (or paying off) never moves its lock date and
        // unlocks it for whoever has it.
        public static void LeaveLocked<T>(List<T>? records, Func<T, long> accountNo, IReadOnlySet<long>? locked)
        {
            if (records == null || locked == null || locked.Count == 0) return;
            records.RemoveAll(r => locked.Contains(accountNo(r)));
        }

        public static bool IsOutcome(string? outcome) =>
            outcome is LostStolenOutcome.Found or LostStolenOutcome.PaidOff or LostStolenOutcome.WrittenOff;

        public static string SafeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName ?? "");
            var clean = new string(name.Where(c => !char.IsControl(c) && c is not ('"' or '\\' or '/')).ToArray()).Trim();
            if (clean.Length == 0) clean = "evidence";
            return clean.Length > 200 ? clean[^200..] : clean;
        }

        private static bool StartsWith(byte[] content, params byte[] magic) =>
            content.Length >= magic.Length && magic.Select((b, i) => content[i] == b).All(x => x);
    }
}
