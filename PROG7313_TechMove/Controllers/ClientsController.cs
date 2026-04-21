using Microsoft.AspNetCore.Mvc;
using PROG7313_TechMove.Models;
using PROG7313_TechMove.Patterns.Repository;
namespace PROG7313_TechMove.Controllers
{
    public class ClientsController : Controller
    {
        private readonly IClientRepository _clientRepo;
        private readonly ILogger<ClientsController> _logger;

        public ClientsController(IClientRepository clientRepo, ILogger<ClientsController> logger)
        {
            _clientRepo = clientRepo;
            _logger = logger;
        }

        // GET: /Clients
        public async Task<IActionResult> Index()
        {
            var clients = await _clientRepo.GetAllAsync();
            return View(clients);
        }

        // GET: /Clients/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var client = await _clientRepo.GetByIdAsync(id);
            if (client == null) return NotFound();
            return View(client);
        }

        // GET: /Clients/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Clients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Client client)
        {
            if (!ModelState.IsValid)
                return View(client);

            await _clientRepo.AddAsync(client);
            TempData["Success"] = $"Client '{client.Name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
