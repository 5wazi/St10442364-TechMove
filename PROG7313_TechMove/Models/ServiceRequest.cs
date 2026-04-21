using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PROG7313_TechMove.Models
{
    public enum ServiceRequestStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled
    }
    public class ServiceRequest
    {
        public int Id { get; set; }

        [Required]
        public int ContractId { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Cost in USD as entered by user
        /// </summary>
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Cost must be greater than zero.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostUsd { get; set; }

        /// <summary>
        /// Converted ZAR cost (saved after currency conversion)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostZar { get; set; }


        /// Exchange rate used at time of creation

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRateUsed { get; set; }

        [Required]
        public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("ContractId")]
        public Contract? Contract { get; set; }
    }
}
