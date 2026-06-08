using Microsoft.AspNetCore.Mvc;
using PROG7313_TechMove.Services;
using PROG7313_TechMove.ViewModels;

namespace PROG7313_TechMove.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApiClient _api;
        private readonly IConfiguration _config;

        public AccountController(ApiClient api, IConfiguration config)
        {
            _api = api;
            _config = config;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(Request.Cookies["techmove_jwt"]))
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            ViewBag.FirebaseApiKey = _config["Firebase:ApiKey"];
            ViewBag.FirebaseProjectId = _config["Firebase:ProjectId"];
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string firebaseIdToken, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(firebaseIdToken))
            {
                ModelState.AddModelError("", "No token received from Firebase.");
                ViewBag.FirebaseApiKey = _config["Firebase:ApiKey"];
                ViewBag.FirebaseProjectId = _config["Firebase:ProjectId"];
                return View(new LoginViewModel());
            }

            var (ok, jwt, email) = await _api.LoginAsync(firebaseIdToken);

            if (!ok || jwt == null)
            {
                ModelState.AddModelError("",
                    "Authentication failed. Please check your credentials.");
                ViewBag.FirebaseApiKey = _config["Firebase:ApiKey"];
                ViewBag.FirebaseProjectId = _config["Firebase:ProjectId"];
                return View(new LoginViewModel());
            }

            // Store JWT in a proper HTTP-only cookie
            Response.Cookies.Append("techmove_jwt", jwt, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,   // set to true in production with HTTPS
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });

            Response.Cookies.Append("techmove_email", email ?? "", new CookieOptions
            {
                HttpOnly = false,   // email can be readable by JS for display
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });

            Console.WriteLine($"[LOGIN] JWT cookie set for {email}, " +
                              $"length={jwt.Length}");

            TempData["Success"] = $"Welcome, {email}!";
            return Redirect(returnUrl ?? "/");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("techmove_jwt");
            Response.Cookies.Delete("techmove_email");
            TempData["Success"] = "You have been logged out.";
            return RedirectToAction("Login");
        }
    }
}