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

            return RedirectToAction("Index", "Home");
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

                    switch (user.RoleId)
                    {
                        case 1:
                            return RedirectToAction("Index", "Home");
                        case 2:
                            return RedirectToAction("Index", "Home");
                        case 5:
                            return RedirectToAction("Index", "Approver");
                        default:
                            break;
                    }
                    return RedirectToAction("Index", "Home");
                }

                return RedirectToAction("Index"); ;
            }
            catch (Exception)
            {

                return View("Index");
            }
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
