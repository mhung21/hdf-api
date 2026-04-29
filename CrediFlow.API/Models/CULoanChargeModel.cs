using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật khoản phí hợp đồng.</summary>
    public class CULoanChargeModel
    {
        /// <summary>Id phí – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? ChargeId { get; set; }

        [Required]
        public Guid LoanContractId { get; set; }

        public Guid? ScheduleId { get; set; }

        /// <summary>Mã phí: FILE_FEE | INSURANCE | PERIODIC_FEE | LATE_PENALTY | EARLY_SETTLEMENT_PENALTY | OTHER_INCOME | OTHER_EXPENSE</summary>
        [Required]
        public string ChargeCode { get; set; } = null!;

        [Required]
        public string ChargeName { get; set; } = null!;

        public DateOnly ChargeDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly DueDate    { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        public string? Note { get; set; }
    }
}
