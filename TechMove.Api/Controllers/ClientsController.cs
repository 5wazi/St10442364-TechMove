using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.DTOs;
using TechMove.Api.Models;
using TechMove.Api.Patterns.Repository;

namespace TechMove.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class ClientsController : ControllerBase
    {
        private readonly IClientRepository _clientRepo;
        private readonly ILogger<ClientsController> _logger;

        public ClientsController(IClientRepository clientRepo, ILogger<ClientsController> logger)
        {
            _clientRepo = clientRepo;
            _logger = logger;
        }

        /// <summary>Get all clients, ordered by name.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ClientDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var clients = await _clientRepo.GetAllAsync();
            var dtos = clients.Select(Map);
            return Ok(dtos);
        }

        /// <summary>Get a single client by ID.</summary>
        [HttpGet("{id:int}")]
        //[AllowAnonymous] //TEMP
        [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var client = await _clientRepo.GetByIdAsync(id);
            if (client == null) return NotFound(new { message = $"Client {id} not found." });
            return Ok(Map(client));
        }

        /// <summary>Create a new client.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ClientDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateClientDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var client = new Client
            {
                Name = dto.Name,
                ContactDetails = dto.ContactDetails,
                Region = dto.Region
            };

            await _clientRepo.AddAsync(client);
            _logger.LogInformation("Client '{Name}' created with Id {Id}.", client.Name, client.Id);
            return CreatedAtAction(nameof(GetById), new { id = client.Id }, Map(client));
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ClientDto Map(Client c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            ContactDetails = c.ContactDetails,
            Region = c.Region,
            ContractCount = c.Contracts?.Count ?? 0
        };
    }
}