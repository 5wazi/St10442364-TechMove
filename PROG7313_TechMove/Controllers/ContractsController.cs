using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PROG7313_TechMove.Services;
using PROG7313_TechMove.ViewModels;

namespace PROG7313_TechMove.Controllers
{
    public class ContractsController : Controller
    {
        private readonly ApiClient _api;
        private readonly ILogger<ContractsController> _logger;

        public ContractsController(ApiClient api, ILogger<ContractsController> logger)
        {
            _api = api;
            _logger = logger;
        }

        private IActionResult? RequireAuth()
        {
            if (string.IsNullOrEmpty(Request.Cookies["techmove_jwt"]))
                return RedirectToAction("Login", "Account",
                    new { returnUrl = Request.Path.ToString() });
            return null;
        }

        // GET: /Contracts
        public async Task<IActionResult> Index(
            DateTime? fromDate, DateTime? toDate, string? status)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var contracts = await _api.GetContractsAsync(fromDate, toDate, status);
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            return View(contracts);
        }

        // GET: /Contracts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var contract = await _api.GetContractAsync(id);
            if (contract == null) return NotFound();
            return View(contract);
        }

        // GET: /Contracts/Create
        public async Task<IActionResult> Create()
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            await PopulateClientsDropdownAsync();
            return View();
        }

        // POST: /Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContractCreateViewModel vm)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            // File validation before calling the API
            if (vm.SignedAgreement != null)
            {
                if (!vm.SignedAgreement.FileName.EndsWith(".pdf",
                        StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("SignedAgreement",
                        "Only PDF files are allowed.");

                if (vm.SignedAgreement.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError("SignedAgreement",
                        "File size must be under 5 MB.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateClientsDropdownAsync();
                return View(vm);
            }

            var (ok, error) = await _api.CreateContractAsync(vm);
            if (!ok)
            {
                ModelState.AddModelError("", error ?? "Failed to create contract.");
                await PopulateClientsDropdownAsync();
                return View(vm);
            }

            TempData["Success"] = "Contract created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Contracts/ChangeStatus/5
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var contract = await _api.GetContractAsync(id);
            if (contract == null) return NotFound();

            return View(new ChangeStatusViewModel
            {
                ContractId = contract.Id,
                CurrentStatus = contract.Status
            });
        }

        // POST: /Contracts/ChangeStatus/5
        [HttpPost, ActionName("ChangeStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatusPost(int id, string newStatus)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var ok = await _api.ChangeStatusAsync(id, newStatus);
            if (ok)
                TempData["Success"] = $"Contract status updated to {newStatus}.";
            else
                TempData["Error"] = "Failed to update status. Please try again.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Contracts/Download/5
        public async Task<IActionResult> Download(int id)
        {
            var auth = RequireAuth();
            if (auth != null) return auth;

            var bytes = await _api.DownloadAgreementAsync(id);
            if (bytes == null)
                return NotFound("No signed agreement found for this contract.");

            return File(bytes, "application/pdf", "agreement.pdf");
        }

        // Populates the client dropdown used on the Create form
        private async Task PopulateClientsDropdownAsync()
        {
            var clients = await _api.GetClientsAsync();
            ViewBag.Clients = new SelectList(clients, "Id", "Name");
        }
    }
}