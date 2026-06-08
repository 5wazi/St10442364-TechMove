using System.ComponentModel.DataAnnotations;
using TechMove.Api.Models;

namespace TechMove.Api.DTOs
{
    // ── Clients ──────────────────────────────────────────────────────────────

    public class ClientDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContactDetails { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public int ContractCount { get; set; }
    }

    public class CreateClientDto
    {
        [Required][StringLength(200)] public string Name { get; set; } = string.Empty;
        [Required][StringLength(500)] public string ContactDetails { get; set; } = string.Empty;
        [Required][StringLength(100)] public string Region { get; set; } = string.Empty;
    }

    // ── Contracts ─────────────────────────────────────────────────────────────

    public class ContractDto
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ServiceLevel { get; set; } = string.Empty;
        public string? SignedAgreementFileName { get; set; }
        public bool HasSignedAgreement { get; set; }
        public List<ServiceRequestDto> ServiceRequests { get; set; } = new();
    }

    public class CreateContractDto
    {
        [Required]
        public int ClientId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [StringLength(200)]
        public string ServiceLevel { get; set; } = string.Empty;

        // File upload is handled via multipart – optional
        public IFormFile? SignedAgreement { get; set; }
    }

    public class ChangeStatusDto
    {
        [Required]
        public ContractStatus NewStatus { get; set; }
    }

    // ── Service Requests ──────────────────────────────────────────────────────

    public class ServiceRequestDto
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal CostUsd { get; set; }
        public decimal CostZar { get; set; }
        public decimal ExchangeRateUsed { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateServiceRequestDto
    {
        [Required]
        public int ContractId { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Cost must be greater than zero.")]
        public decimal CostUsd { get; set; }
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    public class FirebaseTokenDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Jwt { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Uid { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    // ── Currency ─────────────────────────────────────────────────────────────

    public class ExchangeRateDto
    {
        public decimal Rate { get; set; }
        public string Base { get; set; } = "USD";
        public string Target { get; set; } = "ZAR";
        public DateTime FetchedAt { get; set; }
    }
}