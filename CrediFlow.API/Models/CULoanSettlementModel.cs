using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật phiếu tất toán hợp đồng.</summary>
    public class CULoanSettlementModel
    {
        /// <summary>Id tất toán – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? SettlementId { get; set; }

        [Required]
        public Guid LoanContractId { get; set; }

        /// <summary>Loại tất toán: ONTIME | EARLY</summary>
        [Required]
        public string SettlementType { get; set; } = null!;

        public DateOnly RequestDate      { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly? SettlementDate  { get; set; }

        public int     ActualElapsedDays              { get; set; }
        public int     ContractTotalDays              { get; set; }
        public decimal CompletionRatio                { get; set; }

        public decimal RemainingPrincipalAmount       { get; set; }
        public decimal AccruedInterestAmount          { get; set; }
        public decimal AccruedPeriodicFeeAmount       { get; set; }
        public decimal UnpaidLatePenaltyAmount        { get; set; }
        public decimal EarlySettlementPenaltyAmount   { get; set; }
        public decimal OtherReceivableAmount          { get; set; }
        public decimal DiscountAmount                 { get; set; }
        public decimal TotalSettlementAmount          { get; set; }

        public string? Note { get; set; }
    }
}
