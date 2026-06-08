using Microsoft.AspNetCore.Mvc;
using PROG7313_TechMove.Services;
using PROG7313_TechMove.ViewModels;

namespace PROG7313_TechMove.Controllers
{
    public class ServiceRequestsController : Controller
    {
        private readonly ApiClient _api;

        public ServiceRequestsController(ApiClient api)
        {
            _api = api;
        }

        private IActionResult? RequireAuth()
        {
            if (string.IsNullOrEmpty(Request.Cookies["techmove_jwt"]))
                return RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path.ToString() });
            return null;
        }

        // GET: /ServiceRequests
        public async Task<IActionResult> Index()
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            // Fetch all contracts and flatten their service requests into one list
            var contracts = await _api.GetContractsAsync();
            var allRequests = contracts
                .SelectMany(c => c.ServiceRequests)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(allRequests);
        }

        // GET: /ServiceRequests/Create?contractId=1
        public async Task<IActionResult> Create(int contractId)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var contract = await _api.GetContractAsync(contractId);
            if (contract == null) return NotFound();

            var rate = await _api.GetExchangeRateAsync();
            ViewBag.Contract = contract;
            ViewBag.CurrentRate = rate;
            return View(new ServiceRequestCreateViewModel { ContractId = contractId });
        }

        // POST: /ServiceRequests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceRequestCreateViewModel vm)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            if (!ModelState.IsValid)
            {
                ViewBag.Contract = await _api.GetContractAsync(vm.ContractId);
                ViewBag.CurrentRate = await _api.GetExchangeRateAsync();
                return View(vm);
            }

            var (ok, error) = await _api.CreateServiceRequestAsync(vm);
            if (!ok)
            {
                ModelState.AddModelError("", error ?? "Failed to submit service request.");
                ViewBag.Contract = await _api.GetContractAsync(vm.ContractId);
                ViewBag.CurrentRate = await _api.GetExchangeRateAsync();
                return View(vm);
            }

            TempData["Success"] = "Service request submitted successfully.";
            return RedirectToAction("Details", "Contracts", new { id = vm.ContractId });
        }

        // GET: /ServiceRequests/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var sr = await _api.GetServiceRequestAsync(id);
            if (sr == null) return NotFound();
            return View(sr);
        }
    }
}