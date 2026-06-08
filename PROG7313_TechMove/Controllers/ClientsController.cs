using Microsoft.AspNetCore.Mvc;
using PROG7313_TechMove.Services;
using PROG7313_TechMove.ViewModels;

namespace PROG7313_TechMove.Controllers
{
    public class ClientsController : Controller
    {
        private readonly ApiClient _api;
        private readonly ILogger<ClientsController> _logger;

        public ClientsController(ApiClient api, ILogger<ClientsController> logger)
        {
            _api = api;
            _logger = logger;
        }

        // Checks for a JWT in session. If missing, redirects to login.
        private IActionResult? RequireAuth()
        {
            if (string.IsNullOrEmpty(Request.Cookies["techmove_jwt"]))
                return RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path.ToString() });
            return null;
        }

        // GET: /Clients
        public async Task<IActionResult> Index()
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var clients = await _api.GetClientsAsync();
            return View(clients);
        }

        // GET: /Clients/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var client = await _api.GetClientAsync(id);
            if (client == null) return NotFound();
            return View(client);
        }

        // GET: /Clients/Create
        public IActionResult Create()
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            return View();
        }

        // POST: /Clients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateClientViewModel vm)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            if (!ModelState.IsValid)
                return View(vm);

            var client = await _api.CreateClientAsync(vm);
            if (client == null)
            {
                ModelState.AddModelError("", "Failed to create client. Please try again.");
                return View(vm);
            }

            TempData["Success"] = $"Client '{client.Name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}