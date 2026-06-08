using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.DTOs;
using TechMove.Api.Models;
using TechMove.Api.Services;

namespace TechMove.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class ServiceRequestsController : ControllerBase
    {
        private readonly IServiceRequestService _srService;
        private readonly ICurrencyService _currencyService;
        private readonly ILogger<ServiceRequestsController> _logger;

        public ServiceRequestsController(
            IServiceRequestService srService,
            ICurrencyService currencyService,
            ILogger<ServiceRequestsController> logger)
        {
            _srService = srService;
            _currencyService = currencyService;
            _logger = logger;
        }

        // ── GET /api/servicerequests/by-contract/{contractId} ─────────────────

        /// <summary>Get all service requests for a given contract.</summary>
        [HttpGet("by-contract/{contractId:int}")]
        [ProducesResponseType(typeof(IEnumerable<ServiceRequestDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByContract(int contractId)
        {
            var requests = await _srService.GetByContractAsync(contractId);
            return Ok(requests.Select(Map));
        }

        // ── GET /api/servicerequests/{id} ─────────────────────────────────────

        /// <summary>Get a single service request by ID.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ServiceRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var sr = await _srService.GetByIdAsync(id);
            if (sr == null) return NotFound(new { message = $"ServiceRequest {id} not found." });
            return Ok(Map(sr));
        }

        // ── POST /api/servicerequests ─────────────────────────────────────────

        /// <summary>
        /// Submit a new service request against an existing contract.
        /// The cost is stored in both USD (as entered) and ZAR (converted via live rate).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ServiceRequestDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] CreateServiceRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var sr = await _srService.CreateAsync(dto.ContractId, dto.Description, dto.CostUsd);
                return CreatedAtAction(nameof(GetById), new { id = sr.Id }, Map(sr));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── GET /api/servicerequests/rate ─────────────────────────────────────

        /// <summary>Return the current USD → ZAR exchange rate.</summary>
        [HttpGet("rate")]
        [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRate()
        {
            var rate = await _currencyService.GetUsdToZarRateAsync();
            return Ok(new ExchangeRateDto
            {
                Rate = rate,
                FetchedAt = DateTime.UtcNow
            });
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ServiceRequestDto Map(ServiceRequest sr) => new()
        {
            Id = sr.Id,
            ContractId = sr.ContractId,
            Description = sr.Description,
            CostUsd = sr.CostUsd,
            CostZar = sr.CostZar,
            ExchangeRateUsed = sr.ExchangeRateUsed,
            Status = sr.Status.ToString(),
            CreatedAt = sr.CreatedAt
        };
    }
}