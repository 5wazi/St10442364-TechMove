using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PROG7313_TechMove.Models;
using PROG7313_TechMove.Patterns.Repository;
using PROG7313_TechMove.Services;
using PROG7313_TechMove.ViewModels;

namespace PROG7313_TechMove.Controllers
{
    public class ContractsController : Controller
    {

        private readonly IContractService _contractService;
        private readonly IContractRepository _contractRepo;
        private readonly IClientRepository _clientRepo;
        private readonly IFileService _fileService;
        private readonly ILogger<ContractsController> _logger;

        public ContractsController(
            IContractService contractService,
            IContractRepository contractRepo,
            IClientRepository clientRepo,
            IFileService fileService,
            ILogger<ContractsController> logger)
        {
            _contractService = contractService;
            _contractRepo = contractRepo;
            _clientRepo = clientRepo;
            _fileService = fileService;
            _logger = logger;
        }

        // GET: /Contracts
        public async Task<IActionResult> Index(
            DateTime? fromDate, DateTime? toDate, ContractStatus? status)
        {
            var contracts = await _contractService.SearchContractsAsync(fromDate, toDate, status);
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            return View(contracts);
        }

        // GET: /Contracts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var contract = await _contractRepo.GetByIdAsync(id);
            if (contract == null) return NotFound();
            return View(contract);
        }

        // GET: /Contracts/Create
        public async Task<IActionResult> Create()
        {
            await PopulateClientsDropdownAsync();
            return View();
        }

        // POST: /Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContractCreateViewModel vm)
        {
            // File validation
            if (vm.SignedAgreement != null)
            {
                if (!vm.SignedAgreement.FileName.EndsWith(".pdf"))
                {
                    ModelState.AddModelError("SignedAgreement", "Only PDF files are allowed.");
                }

                if (vm.SignedAgreement.Length > 5 * 1024 * 1024) // 5MB
                {
                    ModelState.AddModelError("SignedAgreement", "File size must be under 5MB.");
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateClientsDropdownAsync();
                return View(vm);
            }

            try
            {
                await _contractService.CreateContractAsync(
                    vm.ClientId, vm.StartDate, vm.EndDate,
                    vm.ServiceLevel, vm.SignedAgreement);

                TempData["Success"] = "Contract created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateClientsDropdownAsync();
                return View(vm);
            }
        }

        // GET: /Contracts/ChangeStatus/5
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var contract = await _contractRepo.GetByIdAsync(id);
            if (contract == null) return NotFound();
            return View(contract);
        }

        // POST: /Contracts/ChangeStatus/5
        [HttpPost, ActionName("ChangeStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatusPost(int id, ContractStatus newStatus)
        {
            try
            {
                await _contractService.ChangeStatusAsync(id, newStatus);
                TempData["Success"] = $"Contract status updated to {newStatus}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Contracts/Download/5
        public async Task<IActionResult> Download(int id)
        {
            var contract = await _contractRepo.GetByIdAsync(id);
            if (contract == null || contract.SignedAgreementPath == null)
                return NotFound("No signed agreement found for this contract.");

            var physPath = _fileService.GetPhysicalPath(contract.SignedAgreementPath);
            if (!System.IO.File.Exists(physPath))
                return NotFound("File not found on server.");

            var bytes = await System.IO.File.ReadAllBytesAsync(physPath);
            return File(bytes, "application/pdf", contract.SignedAgreementFileName ?? "agreement.pdf");
        }

        private async Task PopulateClientsDropdownAsync()
        {
            var clients = await _clientRepo.GetAllAsync();
            ViewBag.Clients = new SelectList(clients, "Id", "Name");
        }
    }
}
