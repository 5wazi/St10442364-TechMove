using System.ComponentModel.DataAnnotations;

namespace PROG7313_TechMove.ViewModels
{
    public class ContractCreateViewModel
    {
        [Required]
        [Display(Name = "Client")]
        public int ClientId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1);

        [Required]
        [Display(Name = "Service Level")]
        public string ServiceLevel { get; set; } = "Bronze";

        [Display(Name = "Signed Agreement (PDF)")]
        [DataType(DataType.Upload)]
        public IFormFile? SignedAgreement { get; set; }
    }

    public class ServiceRequestCreateViewModel
    {
        [Required]
        public int ContractId { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Cost (USD) is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Cost must be greater than zero.")]
        [Display(Name = "Cost (USD)")]
        public decimal CostUsd { get; set; }
    }
}
