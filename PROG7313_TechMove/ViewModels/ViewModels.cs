using System.ComponentModel.DataAnnotations;

namespace PROG7313_TechMove.ViewModels
{
    // ─────────────────────────────────────────────────────────────────────────
    // These ViewModels mirror the JSON shapes returned by TechMove.Api.
    // They contain NO EF Core, NO database references, NO model attributes
    // from the old domain layer. They are plain C# classes used only for
    // passing data between the API responses and the Razor views.
    // ─────────────────────────────────────────────────────────────────────────

    // ── Client ViewModels ─────────────────────────────────────────────────────

    /// <summary>
    /// Represents a client as returned by GET /api/clients.
    /// Used on the Clients Index and Details views.
    /// </summary>
    public class ClientViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContactDetails { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public int ContractCount { get; set; }
    }

    /// <summary>
    /// Form model for the Create Client page.
    /// Posted to POST /api/clients via ApiClient.
    /// </summary>
    public class CreateClientViewModel
    {
        [Required(ErrorMessage = "Client name is required.")]
        [StringLength(200)]
        [Display(Name = "Client Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact details are required.")]
        [StringLength(500)]
        [Display(Name = "Contact Details")]
        public string ContactDetails { get; set; } = string.Empty;

        [Required(ErrorMessage = "Region is required.")]
        [StringLength(100)]
        [Display(Name = "Region")]
        public string Region { get; set; } = string.Empty;
    }

    // ── Contract ViewModels ───────────────────────────────────────────────────

    /// <summary>
    /// Represents a contract as returned by GET /api/contracts.
    /// Status is a plain string ("Active", "Draft", etc.) because
    /// the Web project no longer references the ContractStatus enum.
    /// </summary>
    public class ContractViewModel
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>"Draft", "Active", "Expired" or "OnHold"</summary>
        public string Status { get; set; } = string.Empty;

        public string ServiceLevel { get; set; } = string.Empty;
        public string? SignedAgreementFileName { get; set; }
        public bool HasSignedAgreement { get; set; }

        public List<ServiceRequestViewModel> ServiceRequests { get; set; } = new();
    }

    /// <summary>
    /// Form model for the Create Contract page.
    /// Sent to POST /api/contracts via ApiClient as multipart/form-data.
    /// </summary>
    public class ContractCreateViewModel
    {
        [Required(ErrorMessage = "Please select a client.")]
        [Display(Name = "Client")]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1);

        [Required(ErrorMessage = "Service level is required.")]
        [StringLength(200)]
        [Display(Name = "Service Level")]
        public string ServiceLevel { get; set; } = "Bronze";

        [Display(Name = "Signed Agreement (PDF, max 5 MB)")]
        [DataType(DataType.Upload)]
        public IFormFile? SignedAgreement { get; set; }
    }

    /// <summary>
    /// Used on the Change Status page.
    /// The NewStatus is a plain string posted back to the controller,
    /// which then calls ApiClient.ChangeStatusAsync.
    /// </summary>
    public class ChangeStatusViewModel
    {
        public int ContractId { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a new status.")]
        [Display(Name = "New Status")]
        public string NewStatus { get; set; } = string.Empty;
    }

    // ── Service Request ViewModels ────────────────────────────────────────────

    /// <summary>
    /// Represents a service request as returned by the API.
    /// Used on the ServiceRequests Index and Details views.
    /// </summary>
    public class ServiceRequestViewModel
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal CostUsd { get; set; }
        public decimal CostZar { get; set; }
        public decimal ExchangeRateUsed { get; set; }

        /// <summary>"Pending", "InProgress", "Completed" or "Cancelled"</summary>
        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // Populated when the service request is loaded alongside its contract
        public string ContractClientName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Form model for the Create Service Request page.
    /// Posted to POST /api/servicerequests via ApiClient.
    /// </summary>
    public class ServiceRequestCreateViewModel
    {
        [Required]
        public int ContractId { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Cost (USD) is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Cost must be greater than zero.")]
        [Display(Name = "Cost (USD)")]
        public decimal CostUsd { get; set; }
    }

    // ── Auth ViewModels ───────────────────────────────────────────────────────

    /// <summary>
    /// Used on the Login page form.
    /// The actual authentication is done by the Firebase JS SDK —
    /// this ViewModel is only used for server-side validation fallback.
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}