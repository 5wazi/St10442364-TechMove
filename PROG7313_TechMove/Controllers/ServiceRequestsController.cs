using Microsoft.AspNetCore.Mvc;
using PROG7313_TechMove.Models;
using PROG7313_TechMove.Patterns.Repository;
using PROG7313_TechMove.Services;
using PROG7313_TechMove.ViewModels;

namespace PROG7313_TechMove.Controllers
{
    public class ServiceRequestsController : Controller
    {
        private readonly IServiceRequestService _srService;
        private readonly IServiceRequestRepository _srRepo;
        private readonly IContractRepository _contractRepo;
        private readonly ICurrencyService _currencyService;

        public ServiceRequestsController(
            IServiceRequestService srService,
            IServiceRequestRepository srRepo,
            IContractRepository contractRepo,
            ICurrencyService currencyService)
        {
            _srService = srService;
            _srRepo = srRepo;
            _contractRepo = contractRepo;
            _currencyService = currencyService;
        }

        // GET: /ServiceRequests
        public async Task<IActionResult> Index()
        {
            var contracts = await _contractRepo.GetAllAsync();
            var requests = new List<ServiceRequest>();
            foreach (var c in contracts)
            {
                var srs = await _srRepo.GetByContractIdAsync(c.Id);
                requests.AddRange(srs);
            }
            return View(requests.OrderByDescending(r => r.CreatedAt));
        }

        // GET: /ServiceRequests/Create?contractId=1
        public async Task<IActionResult> Create(int contractId)
        {
            var contract = await _contractRepo.GetByIdAsync(contractId);
            if (contract == null) return NotFound();

            decimal rate;
            try { rate = await _currencyService.GetUsdToZarRateAsync(); }
            catch { rate = 18.50m; }

            ViewBag.Contract = contract;
            ViewBag.CurrentRate = rate;
            return View(new ServiceRequestCreateViewModel { ContractId = contractId });
        }

        // POST: /ServiceRequests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceRequestCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Contract = await _contractRepo.GetByIdAsync(vm.ContractId);
                ViewBag.CurrentRate = await _currencyService.GetUsdToZarRateAsync();
                return View(vm);
            }

            try
            {
                await _srService.CreateAsync(vm.ContractId, vm.Description, vm.CostUsd);
                TempData["Success"] = "Service request submitted successfully.";
                return RedirectToAction("Details", "Contracts", new { id = vm.ContractId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.Contract = await _contractRepo.GetByIdAsync(vm.ContractId);
                ViewBag.CurrentRate = await _currencyService.GetUsdToZarRateAsync();
                return View(vm);
            }
        }

        // GET: /ServiceRequests/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var sr = await _srService.GetByIdAsync(id);
            if (sr == null) return NotFound();
            return View(sr);
        }
    }
}
