using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật hợp đồng bảo hiểm.</summary>
    public class CUInsuranceContractModel
    {
        /// <summary>Id bảo hiểm – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? InsuranceContractId { get; set; }

        [Required]
        public Guid LoanContractId { get; set; }

        [Required]
        public string ProviderName  { get; set; } = null!;
        public string? PolicyNumber { get; set; }
        public string? InsuranceName{ get; set; }

        public bool     IsMandatory     { get; set; } = true;
        public decimal  PremiumAmount   { get; set; }
        public decimal? CoverageAmount  { get; set; }

        [Required]
        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo  { get; set; }

        /// <summary>Trạng thái: ACTIVE | EXPIRED | CANCELLED</summary>
        public string StatusCode { get; set; } = InsuranceContractStatus.Active;

        public string? Note { get; set; }
    }
}
