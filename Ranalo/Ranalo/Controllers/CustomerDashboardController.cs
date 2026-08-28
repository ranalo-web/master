using Microsoft.AspNetCore.Mvc;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class CustomerDashboardController : Controller
    {
        [HttpGet]
        [Route("my-device")]
        public IActionResult Index()
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (settings.RoleId != UserRole.Customer && settings.RoleId != UserRole.Admin)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.BackLink = "my-device";
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = false;
            ViewBag.IsDealer = false;
            ViewBag.IsCustomer = settings.RoleId == UserRole.Customer;
            ViewBag.UserName = settings.KnownAs;

            var model = CustomerDashboardSampleData.Build();

            return View(model);
        }
    }

    // Sample data matching the agreed design mockup. Wiring to real services is a follow-up step.
    public static class CustomerDashboardSampleData
    {
        public static CustomerDashboardViewModel Build()
        {
            return new CustomerDashboardViewModel
            {
                CustomerName = "Susan Wanjala",
                ContractNumber = "RC-2026-04821",
                DeviceModel = "iPhone 12",
                DeviceStorage = "128GB",
                DeviceColor = "Space Grey",
                Imei = "356938035643809",
                DeviceId = "RC-DEV-004821",
                DealerName = "Nairobi Mobile Hub",
                AgentName = "Faith Wangari",
                Status = "Active & Unlocked",

                TotalLoanAmount = 62400m,
                ContractStart = "10 Jan 2026",
                ContractEnd = "10 Jan 2027",
                RestructureDate = "None on record",

                DailyPayment = 173m,
                MonthlyInstallment = 5200m,
                NextPaymentDue = "10 Sep 2026",
                NextPaymentDaysAway = "14 days away",
                LastLockDate = "Never locked",
                NextLockDate = "20 Sep 2026 (if Sep 10 payment isn't made)",

                PaidToDate = 36400m,
                BalanceRemaining = 26000m,
                PercentComplete = 58m,
                InstallmentsPaid = 7,
                InstallmentsTotal = 12,

                RecentPayments = new List<CustomerPayment>
                {
                    new() { DatePaid = "06 Aug 2026", Amount = 5200, Method = "M-Pesa", Status = "Paid", BalanceAfter = 26000 },
                    new() { DatePaid = "08 Jul 2026", Amount = 5200, Method = "M-Pesa", Status = "Paid", BalanceAfter = 31200 },
                    new() { DatePaid = "09 Jun 2026", Amount = 5200, Method = "M-Pesa", Status = "Paid", BalanceAfter = 36400 },
                    new() { DatePaid = "10 May 2026", Amount = 5200, Method = "M-Pesa", Status = "Paid", BalanceAfter = 41600 },
                    new() { DatePaid = "09 Apr 2026", Amount = 5200, Method = "M-Pesa", Status = "Paid", BalanceAfter = 46800 },
                    new() { DatePaid = "08 Mar 2026", Amount = 5200, Method = "M-Pesa", Status = "Paid", BalanceAfter = 52000 },
                    new() { DatePaid = "07 Feb 2026", Amount = 5200, Method = "M-Pesa", Status = "Paid", BalanceAfter = 57200 },
                },
            };
        }
    }
}
