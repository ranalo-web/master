using System.Globalization;

namespace Ranalo.Services
{
    // Greetings sent on their own (ScheduledCelebrationMessages, 2026-11-09):
    // Kenya's yearly public holidays, the customer's birthday, and "halfway"
    // once half the phone is paid. Each is logged under its own MessageType,
    // which is how no one gets the same greeting twice, and shows in the
    // app's Notifications like every message.
    public static class CelebrationRules
    {
        public const string HolidayPrefix = "Holiday:";
        public const string BirthdayPrefix = "Birthday:";
        public const string HalfwayType = "Halfway";

        // Sent only in daytime, Kenya time: never a greeting at night.
        public const int FirstHourKenya = 9;
        public const int LastHourKenya = 19;

        // "Halfway" goes to accounts between these shares paid, so someone
        // already at 90% when this starts isn't told they're halfway.
        public const decimal HalfwayFrom = 0.50m;
        public const decimal HalfwayTo = 0.60m;

        public static bool SendingHours(DateTime nowKenya) =>
            nowKenya.Hour >= FirstHourKenya && nowKenya.Hour < LastHourKenya;

        public record Holiday(string Key, DateTime Date, string Title, string Text);

        // Kenya's public holidays that come every year. Good Friday and Easter
        // Monday are one Easter greeting, on Easter Sunday; Boxing Day is part
        // of Christmas. Eid al-Fitr moves with the moon, so its dates come from
        // settings (Celebrations:EidAlFitr), confirmed each year.
        public static List<Holiday> HolidaysIn(int year, IEnumerable<DateTime>? eidAlFitr = null)
        {
            var list = new List<Holiday>
            {
                new("NewYear", new DateTime(year, 1, 1), "Happy New Year from Ranalo Credit",
                    $"Hi {{name}}, happy New Year! Thank you for being with us. We wish you and your family a great {year}."),
                new("Easter", EasterSunday(year), "Happy Easter from Ranalo Credit",
                    "Hi {name}, happy Easter! We wish you and your family a peaceful and joyful Easter."),
                new("LabourDay", new DateTime(year, 5, 1), "Happy Labour Day",
                    "Hi {name}, happy Labour Day! Thank you for your hard work. Enjoy the holiday."),
                new("Madaraka", new DateTime(year, 6, 1), "Happy Madaraka Day",
                    "Hi {name}, happy Madaraka Day! Enjoy the holiday with your family."),
                new("Mazingira", new DateTime(year, 10, 10), "Happy Mazingira Day",
                    "Hi {name}, happy Mazingira Day! Plant a tree, care for our environment, and enjoy the holiday."),
                new("Mashujaa", new DateTime(year, 10, 20), "Happy Mashujaa Day",
                    "Hi {name}, happy Mashujaa Day! Today we celebrate the heroes of Kenya. Enjoy the holiday."),
                new("Jamhuri", new DateTime(year, 12, 12), "Happy Jamhuri Day",
                    "Hi {name}, happy Jamhuri Day! Enjoy the holiday with your family."),
                new("Christmas", new DateTime(year, 12, 25), "Merry Christmas from Ranalo Credit",
                    "Hi {name}, merry Christmas! We wish you and your family a happy holiday season. Thank you for being with us this year."),
            };

            foreach (var eid in (eidAlFitr ?? Enumerable.Empty<DateTime>()).Where(d => d.Year == year))
            {
                list.Add(new("EidAlFitr", eid.Date, "Eid Mubarak from Ranalo Credit",
                    "Hi {name}, Eid Mubarak! We wish you and your family a blessed Eid."));
            }

            return list.OrderBy(h => h.Date).ToList();
        }

        public static Holiday? HolidayOn(DateTime kenyaDate, IEnumerable<DateTime>? eidAlFitr = null) =>
            HolidaysIn(kenyaDate.Year, eidAlFitr).FirstOrDefault(h => h.Date == kenyaDate.Date);

        public static string HolidayType(Holiday h) =>
            HolidayPrefix + h.Key + "-" + h.Date.Year.ToString(CultureInfo.InvariantCulture);

        // Western Easter Sunday (the anonymous Gregorian algorithm).
        public static DateTime EasterSunday(int year)
        {
            int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4;
            int f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateTime(year, month, day);
        }

        // ---------- Birthdays ----------

        public static string BirthdayType(int year) =>
            BirthdayPrefix + year.ToString(CultureInfo.InvariantCulture);

        // The date of birth from the WooCommerce application, typed as text.
        // Kenya writes day first, so 03/07/1990 is 3 July. null = can't tell.
        private static readonly string[] DobFormats =
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
            "dd.MM.yyyy", "d.M.yyyy", "MMMM d, yyyy", "d MMMM yyyy", "d MMM yyyy",
        };

        public static DateTime? ParseDob(string? raw, DateTime today)
        {
            var text = raw?.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            // "1990-07-03T00:00:00" and "1990-07-03 00:00:00": the date part.
            if (text.Length > 10 && text[4] == '-' && (text[10] == 'T' || text[10] == ' ')) text = text[..10];

            if (!DateTime.TryParseExact(text, DobFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
                return null;
            // An adult, and not a typo like 1890 or this year.
            var age = today.Year - dob.Year;
            return age >= 16 && age <= 100 ? dob.Date : null;
        }

        // 29 February birthdays are kept on 28 February in other years.
        public static bool IsBirthday(DateTime dob, DateTime kenyaDate) =>
            dob.Month == kenyaDate.Month && (dob.Day == kenyaDate.Day
                || (dob.Month == 2 && dob.Day == 29 && kenyaDate.Day == 28 && !DateTime.IsLeapYear(kenyaDate.Year)));

        public static CustomerMessages.Message Birthday(string? firstName) =>
            new("Happy birthday from Ranalo Credit",
                $"Happy birthday {CustomerMessages.Name(firstName)}! Everyone at Ranalo Credit wishes you a wonderful day and a great year ahead.");

        // ---------- Halfway ----------

        public static bool IsHalfway(decimal paid, decimal owed) =>
            owed > 0 && paid >= owed * HalfwayFrom && paid < owed * HalfwayTo;

        public static CustomerMessages.Message Halfway(string? firstName, long accountNo) =>
            new("You're halfway there!",
                $"Well done {CustomerMessages.Name(firstName)}! You have now paid more than half of your phone on account {accountNo}. " +
                "Keep paying every day and the phone will be fully yours.\n\n" +
                $"Help: call {CustomerMessages.HelpLine}");

        public static CustomerMessages.Message For(Holiday h, string? firstName) =>
            new(h.Title, h.Text.Replace("{name}", CustomerMessages.Name(firstName)));
    }
}
