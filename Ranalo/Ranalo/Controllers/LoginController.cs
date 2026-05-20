using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Ranalo.DataStore.DataModels;
using Ranalo.Services;
using System.Text.Json;
using static Dapper.SqlMapper;

namespace Ranalo.Controllers
{
    public class LoginController : Controller
    {
        private readonly IUserService _usersService;
        public LoginController(IUserService usersService)
        {
            _usersService = usersService;
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index()
            {
            var cookie = Request.Cookies["UserSettings"];
            if (string.IsNullOrEmpty(cookie))
            {
                return View();

            }

            var cookieValue = CookieHelper.Deserialize<User>(cookie);

            if (cookieValue == null)
            {
                return View();
            }

            // Optional: set it to HttpContext.Items if you want to use it elsewhere
            HttpContext.Items["UserSettings"] = cookieValue;

            switch (cookieValue.RoleId)
            {
                case UserRole.Admin:
                    return RedirectToAction("Index", "Home");
                case UserRole.Dealer:
                    return Redirect("/Index");
                case UserRole.Approver:
                    return RedirectToAction("Index", "Approver");
                case UserRole.Collector:
                    return RedirectToAction("Index", "Collections");
                case UserRole.Agent:
                    return RedirectToAction("Index", "Agents");
                default:
                    break;
            }

            return Redirect("/Index");
        }

        [HttpPost]
        [Route("")]
        public async Task<IActionResult> Login(string email, string password, string rememberme)
        {
            try
            {
                // Get user by email and password
                var user = await _usersService.LoginUser(email, password);
                if (user != null)
                {
                    //Set the user cookie
                    var cookieValue = CookieHelper.Serialize(user);

                    Response.Cookies.Append("UserSettings", cookieValue, new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        HttpOnly = true,
                        Secure = true,
                        IsEssential = true,
                        SameSite = SameSiteMode.Strict
                    });

                    ViewBag.BackLink = "dashboard";
                    ViewBag.IsAdmin = user.RoleId == UserRole.Admin;
                    ViewBag.IsApprover = user.RoleId == UserRole.Approver;
                    ViewBag.IsDealer = user.RoleId == UserRole.Dealer;
                    ViewBag.IsCollector = user.RoleId == UserRole.Collector;
                    ViewBag.IsAgent = user.RoleId == UserRole.Agent;


                    switch (user.RoleId)
                    {
                        case UserRole.Admin:
                            return RedirectToAction("Index", "Home");
                        case UserRole.Dealer:
                            return Redirect("/Index");
                        case UserRole.Approver:
                            return RedirectToAction("Index", "Approver");
                        case UserRole.Collector:
                            return RedirectToAction("Index", "Collections");
                        case UserRole.Agent:
                            return RedirectToAction("Index", "Agent");
                        default:
                            break;
                    }
                    return RedirectToAction("Index", "Login");
                }

                return RedirectToAction("Index");
            }
            catch (Exception)
            {

                return View("Index");
            }
        }


        [HttpPost]
        [Route("reset")]
        public async Task<IActionResult> ResetPassword(string email, string oldpassword, string newpassword)
        {
            try
            {
                // Get user by email and password
                var user = await _usersService.LoginUser(email, oldpassword);
                if (user != null)
                {
                    var updatedUser = await _usersService.UpdateUserPasswordAsync(user.UserId, newpassword);
                    //Set the user cookie
                    var cookieValue = CookieHelper.Serialize(user);

                    Response.Cookies.Append("UserSettings", cookieValue, new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        HttpOnly = true,
                        Secure = true,
                        IsEssential = true,
                        SameSite = SameSiteMode.Strict
                    });

                    ViewBag.BackLink = "Index";
                    ViewBag.IsAdmin = user.RoleId == UserRole.Admin;
                    ViewBag.IsApprover = user.RoleId == UserRole.Approver;
                    ViewBag.IsDealer = user.RoleId == UserRole.Dealer;


                    return View("ResetSuccess");
                }

                return RedirectToAction("Index"); ;
            }
            catch (Exception)
            {

                return View("Index");
            }
        }

        [HttpGet]
        [Route("reset")]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpGet]
        [Route("reset-success")]
        public IActionResult ResetSuccess()
        {
            return RedirectToAction("Index");
        }

        [HttpGet]
        [Route("logout")]
        public IActionResult Logout()
        {
            // Delete the UserSettings cookie
            Response.Cookies.Delete("UserSettings");

            // Redirect to login or home page
            return RedirectToAction("Index", "Login");
        }

        [Route("signup")]
        public IActionResult Signup()
        {
            return View("Signup");
        }
    }
}
