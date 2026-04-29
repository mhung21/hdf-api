using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model cập nhật hồ sơ nợ xấu (tạo mới do hệ thống tự sinh khi hợp đồng chuyển BAD_DEBT).</summary>
    public class CUBadDebtCaseModel
    {
        /// <summary>Id hồ sơ nợ xấu – null khi tạo mới (thủ công), có giá trị khi cập nhật.</summary>
        public Guid? BadDebtCaseId { get; set; }

        [Required]
        public Guid LoanContractId { get; set; }

        [Required]
        public Guid StoreId { get; set; }

        public decimal OutstandingPrincipalAmount    { get; set; }
        public decimal OutstandingInterestAmount     { get; set; }
        public decimal OutstandingPeriodicFeeAmount  { get; set; }
        public decimal OutstandingLatePenaltyAmount  { get; set; }
        public decimal OtherOutstandingAmount        { get; set; }
        public decimal TotalOutstandingAmount        { get; set; }
        public decimal RecoveredAmountTotal          { get; set; }

        /// <summary>Trạng thái: OPEN | RECOVERING | CLOSED</summary>
        public string StatusCode { get; set; } = BadDebtCaseStatus.Open;

        public string? Note { get; set; }
    }

    /// <summary>Model ghi nhận khoản thu hồi nợ xấu — tạo phiếu thu BAD_DEBT_RECOVERY.</summary>
    public class RecordBadDebtRecoveryModel
    {
        [Required]
        public Guid BadDebtCaseId { get; set; }

        [Required, Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0.")]
        public decimal Amount { get; set; }

        public DateOnly BusinessDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        /// <summary>Ghi chú / lý do thu hồi.</summary>
        public string? Note { get; set; }
    }
}
