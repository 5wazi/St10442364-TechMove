using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.DTOs;
using TechMove.Api.Models;
using TechMove.Api.Patterns.Repository;
using TechMove.Api.Services;

namespace TechMove.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class ContractsController : ControllerBase
    {
        private readonly IContractService _contractService;
        private readonly IContractRepository _contractRepo;
        private readonly IFileService _fileService;
        private readonly ILogger<ContractsController> _logger;

        public ContractsController(
            IContractService contractService,
            IContractRepository contractRepo,
            IFileService fileService,
            ILogger<ContractsController> logger)
        {
            _contractService = contractService;
            _contractRepo = contractRepo;
            _fileService = fileService;
            _logger = logger;
        }

        // ── GET /api/contracts?fromDate=&toDate=&status= ──────────────────────

        /// <summary>
        /// List all contracts with optional date-range and status filters.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ContractDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] ContractStatus? status)
        {
            var contracts = await _contractService.SearchContractsAsync(fromDate, toDate, status);
            return Ok(contracts.Select(Map));
        }

        // ── GET /api/contracts/{id} ───────────────────────────────────────────

        /// <summary>Get a single contract with its service requests.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var contract = await _contractRepo.GetByIdAsync(id);
            if (contract == null) return NotFound(new { message = $"Contract {id} not found." });
            return Ok(Map(contract));
        }

        // ── POST /api/contracts ───────────────────────────────────────────────

        /// <summary>
        /// Create a new contract. Accepts multipart/form-data so that a PDF
        /// signed agreement can optionally be uploaded at the same time.
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ContractDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromForm] CreateContractDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Extra file validations (mirrors the MVC controller)
            if (dto.SignedAgreement != null)
            {
                if (!dto.SignedAgreement.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Only PDF files are allowed." });

                if (dto.SignedAgreement.Length > 5 * 1024 * 1024)
                    return BadRequest(new { message = "File size must be under 5 MB." });
            }

            try
            {
                var contract = await _contractService.CreateContractAsync(
                    dto.ClientId, dto.StartDate, dto.EndDate,
                    dto.ServiceLevel, dto.SignedAgreement);

                return CreatedAtAction(nameof(GetById), new { id = contract.Id }, Map(contract));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── PATCH /api/contracts/{id}/status ─────────────────────────────────

        /// <summary>
        /// Change a contract's status (e.g. Approve / Decline / Put on Hold).
        /// </summary>
        [HttpPatch("{id:int}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _contractService.ChangeStatusAsync(id, dto.NewStatus);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ── GET /api/contracts/{id}/download ─────────────────────────────────

        /// <summary>Download the signed agreement PDF for a contract.</summary>
        [HttpGet("{id:int}/download")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Download(int id)
        {
            var contract = await _contractRepo.GetByIdAsync(id);
            if (contract?.SignedAgreementPath == null)
                return NotFound(new { message = "No signed agreement found for this contract." });

            var physPath = _fileService.GetPhysicalPath(contract.SignedAgreementPath);
            if (!System.IO.File.Exists(physPath))
                return NotFound(new { message = "File not found on server." });

            var bytes = await System.IO.File.ReadAllBytesAsync(physPath);
            return File(bytes, "application/pdf",
                        contract.SignedAgreementFileName ?? "agreement.pdf");
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ContractDto Map(Contract c) => new()
        {
            Id = c.Id,
            ClientId = c.ClientId,
            ClientName = c.Client?.Name ?? string.Empty,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Status = c.Status.ToString(),
            ServiceLevel = c.ServiceLevel,
            SignedAgreementFileName = c.SignedAgreementFileName,
            HasSignedAgreement = c.SignedAgreementPath != null,
            ServiceRequests = c.ServiceRequests?
                                      .Select(sr => new ServiceRequestDto
                                      {
                                          Id = sr.Id,
                                          ContractId = sr.ContractId,
                                          Description = sr.Description,
                                          CostUsd = sr.CostUsd,
                                          CostZar = sr.CostZar,
                                          ExchangeRateUsed = sr.ExchangeRateUsed,
                                          Status = sr.Status.ToString(),
                                          CreatedAt = sr.CreatedAt
                                      }).ToList() ?? new()
        };
    }
}