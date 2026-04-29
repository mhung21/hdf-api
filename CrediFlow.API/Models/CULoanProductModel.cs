using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật sản phẩm vay.</summary>
    public class CULoanProductModel
    {
        /// <summary>Id sản phẩm – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? LoanProductId { get; set; }

        /// <summary>Chi nhánh áp dụng (null = toàn hệ thống).</summary>
        public Guid? StoreId { get; set; }

        [Required]
        public string ProductCode { get; set; } = null!;

        [Required]
        public string ProductName { get; set; } = null!;

        public string? Description { get; set; }

        public decimal MinPrincipalAmount { get; set; }
        public decimal MaxPrincipalAmount { get; set; }
        public int MinTermMonths          { get; set; } = 1;
        public int MaxTermMonths          { get; set; } = 1;

        /// <summary>Lãi suất tháng, ví dụ 0.016 = 1.6%/tháng.</summary>
        public decimal InterestRateMonthly         { get; set; }
        /// <summary>Phí QLKV (Quản lý khu vực) tháng, ví dụ 0.014 = 1.4%/tháng.</summary>
        public decimal QlkvRateMonthly             { get; set; }
        /// <summary>Phí QLTS (Quản lý tài sản) tháng, ví dụ 0.045 = 4.5%/tháng.</summary>
        public decimal QltsRateMonthly             { get; set; }
        /// <summary>Phí cố định thu theo tháng (VNĐ).</summary>
        public decimal FixedMonthlyFeeAmount       { get; set; }
        public decimal DefaultFileFeeAmount        { get; set; }
        public decimal DefaultInsuranceRate        { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
