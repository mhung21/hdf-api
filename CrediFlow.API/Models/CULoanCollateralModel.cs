using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật tài sản đảm bảo.</summary>
    public class CULoanCollateralModel
    {
        /// <summary>Id đảm bảo – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? CollateralId { get; set; }

        [Required]
        public Guid LoanContractId { get; set; }

        /// <summary>Loại tài sản: PROPERTY | VEHICLE | SAVINGS | OTHER</summary>
        public string CollateralType { get; set; } = "OTHER";

        /// <summary>Mô tả tài sản. VD: "Xe máy Honda Wave RS 2020"</summary>
        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = null!;

        /// <summary>Giá trị ước tính (VNĐ). Để trống nếu chưa định giá.</summary>
        [Range(0, double.MaxValue)]
        public decimal? EstimatedValue { get; set; }

        /// <summary>Chi tiết bổ sung: địa chỉ bất động sản, biển số xe, số seri…</summary>
        [MaxLength(1000)]
        public string? Detail { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }
}
