using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class CustomerCareDashboardController : Controller
    {
        [HttpGet]
        [Route("customer-care-dashboard")]
        public IActionResult Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Customer Care is mapped to the Collector role (closest existing
            // role to a collections/support desk) until a dedicated role exists.
            if (settings.RoleId != UserRole.Collector && settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "customer-care-dashboard";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.IsCustomerCare = settings.RoleId == UserRole.Collector;
            ViewBag.UserName = settings.KnownAs;

            var model = CustomerCareDashboardSampleData.Build();

            return View(model);
        }
    }

    // Sample data matching the agreed design mockup. Wiring to real services is a follow-up step.
    public static class CustomerCareDashboardSampleData
    {
        public static CustomerCareDashboardViewModel Build()
        {
            return new CustomerCareDashboardViewModel
            {
                StaffName = "Mercy Adhiambo",

                CallsDueToday = 18,
                CallsCritical = 4,
                CallsBehind = 14,

                OverdueAccounts = 34,
                TotalArrears = 148200m,

                DevicesLocked = 9,
                UnlockedToday = 3,

                TicketsResolvedToday = 12,
                TicketsOpenToday = 15,

                ExampleLookup = new CareAccountLookup
                {
                    CustomerName = "Susan Wanjala",
                    Status = "Active & Unlocked",
                    Device = "iPhone 12",
                    ContractNumber = "RC-2026-04821",
                    Balance = 26000m,
                    NextDue = "10 Sep 2026",
                    AgentName = "Faith Wangari",
                    DealerName = "Nairobi Mobile Hub",
                },

                CallQueue = new List<CareQueueEntry>
                {
                    new() { Priority = "Critical", CustomerName = "Peter Wanjohi", Phone = "+254 722 456 781", DaysLate = "45 days", Arrears = 5800, Balance = 22000, AgentName = "Brian Kiplangat", DealerName = "Nairobi Mobile Hub" },
                    new() { Priority = "Critical", CustomerName = "Alice Mumbi", Phone = "+254 733 221 904", DaysLate = "38 days", Arrears = 4200, Balance = 18500, AgentName = "Nancy Cherop", DealerName = "Nairobi Mobile Hub" },
                    new() { Priority = "Critical", CustomerName = "Dennis Kamau", Phone = "+254 711 908 442", DaysLate = "34 days", Arrears = 3000, Balance = 27000, AgentName = "Faith Wangari", DealerName = "Nairobi Mobile Hub" },
                    new() { Priority = "Critical", CustomerName = "Ruth Chebet", Phone = "+254 745 302 118", DaysLate = "31 days", Arrears = 2600, Balance = 15600, AgentName = "Faith Wangari", DealerName = "Nairobi Mobile Hub" },
                    new() { Priority = "Behind", CustomerName = "Collins Mwas", Phone = "+254 700 556 823", DaysLate = "12 days", Arrears = 4800, Balance = 31000, AgentName = "Brian Kiplangat", DealerName = "Nairobi Mobile Hub" },
                    new() { Priority = "Behind", CustomerName = "Faith Achieng", Phone = "+254 728 774 390", DaysLate = "9 days", Arrears = 3900, Balance = 19600, AgentName = "Nancy Cherop", DealerName = "Nairobi Mobile Hub" },
                    new() { Priority = "Behind", CustomerName = "Esther Nyambura", Phone = "+254 719 662 057", DaysLate = "6 days", Arrears = 2200, Balance = 14800, AgentName = "Brian Kiplangat", DealerName = "Nairobi Mobile Hub" },
                },

                LockedDevices = new List<CareLockedDevice>
                {
                    new() { CustomerName = "Michael Wafula", Device = "Samsung A15", LockedAgo = "3 days ago", Balance = 24500 },
                    new() { CustomerName = "Grace Njoki", Device = "iPhone 12", LockedAgo = "6 days ago", Balance = 38200 },
                },

                RecentTickets = new List<CareTicket>
                {
                    new() { CustomerName = "Susan Wanjala", Type = "Call", Note = "Confirmed upcoming payment, no issues", StaffName = "Mercy A.", Time = "10:42 AM" },
                    new() { CustomerName = "Peter Wanjohi", Type = "Call", Note = "No answer, left voicemail, will retry tomorrow", StaffName = "Mercy A.", Time = "10:15 AM" },
                    new() { CustomerName = "Esther Nyambura", Type = "SMS", Note = "Sent payment reminder for Sep 1 due date", StaffName = "Kevin B.", Time = "09:50 AM" },
                    new() { CustomerName = "Collins Mwas", Type = "Call", Note = "Promised to pay by Friday, noted in system", StaffName = "Kevin B.", Time = "09:20 AM" },
                },
            };
        }
    }
}
