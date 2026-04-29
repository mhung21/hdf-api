using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật hợp đồng vay.</summary>
    public class CULoanContractModel
    {
        /// <summary>Id hợp đồng – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? LoanContractId { get; set; }

        [Required]
        public Guid StoreId    { get; set; }

        [Required]
        public Guid CustomerId { get; set; }

        /// <summary>Loại hợp đồng: INSTALLMENT hoặc PAWN.</summary>
        [Required]
        public string ContractType { get; set; } = LoanContractType.Installment;

        public Guid? LoanProductId            { get; set; }
        public Guid? PreviousLoanContractId   { get; set; }

        public DateOnly ApplicationDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [Range(1, int.MaxValue)]
        public int TermMonths { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal PrincipalAmount    { get; set; }

        public decimal NetDisbursedAmount { get; set; }

        /// <summary>Lãi suất tháng snapshot (tự điền từ sản phẩm hoặc nhập tay).</summary>
        public decimal InterestRateMonthlySnapshot          { get; set; }
        /// <summary>Phí QLKV snapshot.</summary>
        public decimal QlkvRateMonthlySnapshot              { get; set; }
        /// <summary>Phí QLTS snapshot.</summary>
        public decimal QltsRateMonthlySnapshot              { get; set; }
        /// <summary>Phí cố định thu mỗi tháng (VNĐ).</summary>
        public decimal FixedMonthlyFeeAmountSnapshot        { get; set; }
        public decimal FileFeeAmountSnapshot                { get; set; }
        public decimal InsuranceAmountSnapshot              { get; set; }
        /// <summary>Lãi cầm đồ tính theo VNĐ / 1.000.000 / ngày.</summary>
        public decimal PawnInterestAmountPerMillionPerDaySnapshot { get; set; }
        /// <summary>Phí QLTS cầm đồ tính theo VNĐ / 1.000.000 / ngày.</summary>
        public decimal PawnFeeAmountPerMillionPerDaySnapshot      { get; set; }
        /// <summary>Số ngày mặc định của một kỳ cầm đồ. Workbook mẫu dùng 10 ngày.</summary>
        public short   PawnPeriodDaysSnapshot              { get; set; } = 10;
        /// <summary>% chiết khấu bảo hiểm tại thời điểm tạo hợp đồng. Tự động set từ chính sách khi tạo mới.</summary>
        public decimal InsuranceDiscountRateSnapshot        { get; set; } = 0m;
        public decimal EarlySettlementPenaltyRateSnapshot   { get; set; } = 5m;
        public decimal LatePaymentPenaltyRateSnapshot       { get; set; } = 8m;
        public short   LatePaymentStartDaySnapshot          { get; set; } = 4;
        public short   BadDebtStartDaySnapshot              { get; set; } = 11;

        public string? Note { get; set; }

        /// <summary>Luồng khách (liên kết đến bảng customer_sources).</summary>
        public Guid? CustomerSourceId { get; set; }

        /// <summary>
        /// Trạng thái khi tạo mới. Chỉ chấp nhận:
        /// <c>"DRAFT"</c> – lưu nháp, hoặc
        /// <c>"PENDING_APPROVAL"</c> – gửi cho quản lý duyệt luôn, hoặc
        /// <c>"DISBURSED"</c> – nhập hậu kỳ (hợp đồng đã giải ngân trước khi vào hệ thống; chỉ Manager/Admin).
        /// Khi cập nhật (LoanContractId có giá trị) thì bỏ qua trường này.
        /// </summary>
        public string? InitialStatus { get; set; } = LoanContractStatus.Draft;

        /// <summary>
        /// Ngày giải ngân thực tế để nhập hậu kỳ (chỉ dùng khi InitialStatus = DISBURSED).
        /// Nếu null: dùng ngày hôm nay.
        /// </summary>
        public DateOnly? BackdatedDisbursedDate { get; set; }
    }
}
