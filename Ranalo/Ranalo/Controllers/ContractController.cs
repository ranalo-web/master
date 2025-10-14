using Microsoft.AspNetCore.Mvc;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Configuration;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranalo.Controllers
{
    [LoadUserSettingsFromCookie]
    public class ContractController : Controller
    {
        private readonly IContractService _contractService;
        private readonly IUserService _userService;
        public ContractController(IContractService contractService, IUserService userService    )
        {
            _contractService = contractService;
            _userService = userService;
        }
        [HttpGet]
        [Route("contracts/{page:int?}")]
        public async Task<IActionResult> Index(string searchTerm, int page = 1, int pageSize = 10)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }
            await SetViewBags(settings, "approver");

            if (settings.RoleId == UserRole.Admin || settings.RoleId == UserRole.Approver)
            {
                var contracts = await _contractService.GetAllContractsAsync(page: page, pageSize: pageSize, searchTerm);
                

                ViewData["OrdersStatus"] = "Waiting Approval";
                return View(contracts);
            }

            return View();

        }

        [HttpPost]
        [Route("update-contract")]
        public async Task<IActionResult> UpdateContractDetails(int deviceId, 
            decimal deposit, 
            decimal daily, 
            decimal weekly, 
            decimal monthly, 
            string interval,
            decimal loan,
            decimal cost)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var contractToUpdate = new ContractInfo()
            {
                ID = deviceId,
                Deposit = deposit,
                Daily = daily,
                Weekly = weekly,
                Monthly = monthly,
                RePaymentIntervals = interval,
                TotalCost = cost,
                TotalLoan = loan
            };

            var update = await _contractService.UpdateContractAsync(contractToUpdate);

            return RedirectToAction("Index", "Contract");
        }

        [HttpPost]
        [Route("add-contract")]
        public async Task<IActionResult> AddNewContractDetails(string firstName,
            int deviceId,
            decimal deposit,
            decimal daily,
            decimal weekly,
            decimal monthly,
            string interval,
            decimal loan,
            decimal cost)
        {
            var settings = HttpContext.Items["UserSettings"] as User;
            if (settings == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var contractToUpdate = new ContractInfo()
            {
                ID = deviceId,
                Deposit = deposit,
                Daily = daily,
                Weekly = weekly,
                Monthly = monthly,
                RePaymentIntervals = interval,
                TotalCost = cost,
                TotalLoan = loan
            };

            var update = await _contractService.UpdateContractAsync(contractToUpdate);

            return RedirectToAction("Index", "Contract");
        }
        //update-contract

        private async Task SetViewBags(User settings, string backLink)
        {
            ViewBag.BackLink = backLink;
            ViewBag.IsAdmin = settings.RoleId == UserRole.Admin;
            ViewBag.IsApprover = settings.RoleId == UserRole.Approver;
            ViewBag.IsDealer = settings.RoleId == UserRole.Dealer;
            ViewBag.UserName = settings.KnownAs;
            if (settings.RoleId == UserRole.Dealer)
            {
                var dealer = await _userService.GetDealerByUserId(settings.UserId);
                ViewBag.UserName = dealer.CompanyName;
            }
        }
    }
}
