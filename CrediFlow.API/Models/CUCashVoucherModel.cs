using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật phiếu thu/chi.</summary>
    public class CUCashVoucherModel
    {
        /// <summary>Id phiếu – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? VoucherId { get; set; }

        /// <summary>Chi nhánh – nếu null, service sẽ tự lấy từ JWT của người dùng.</summary>
        public Guid? StoreId { get; set; }

        /// <summary>Loại phiếu: RECEIPT | PAYMENT</summary>
        [Required]
        public string VoucherType { get; set; } = null!;

        /// <summary>Lý do: INSTALLMENT_COLLECTION | EARLY_SETTLEMENT | UPFRONT_FEE_COLLECTION | ...</summary>
        [Required]
        public string ReasonCode { get; set; } = null!;

        public DateOnly BusinessDate     { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateTime VoucherDatetime  { get; set; } = DateTime.Now;

        public Guid? CustomerId      { get; set; }
        public Guid? LoanContractId  { get; set; }
        public Guid? RelatedVoucherId{ get; set; }

        public string? PayerReceiverName    { get; set; }
        public string? PayerReceiverAddress { get; set; }
        public string? DocumentNo           { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public string Description { get; set; } = null!;

        public bool IsAdjustment { get; set; } = false;

        /// <summary>Hình thức thanh toán: CASH | COMPANY_ACCOUNT (chỉ áp dụng cho phiếu chi)</summary>
        public string? PaymentMethod { get; set; }

        /// <summary>Tên ngân hàng (khi chuyển khoản).</summary>
        public string? BankName { get; set; }

        /// <summary>Số tài khoản ngân hàng (khi chuyển khoản).</summary>
        public string? BankAccountNumber { get; set; }
    }

    /// <summary>
    /// Model thu tiền khoản vay – tạo phiếu thu + phân bổ + cập nhật lịch/phí tự động.
    /// </summary>
    public class CollectLoanPaymentModel
    {
        [Required]
        public Guid LoanContractId { get; set; }

        /// <summary>
        /// Các khoản thu: FILE_FEE | INSURANCE | INTEREST | PERIODIC_FEE | PRINCIPAL | LATE_PENALTY | OTHER
        /// </summary>
        [Required, MinLength(1)]
        public List<string> Purposes { get; set; } = new();

        /// <summary>Mã lý do phiếu thu, ví dụ: LOAN_COLLECTION | UPFRONT_FEE_COLLECTION | ...</summary>
        [Required]
        public string ReasonCode { get; set; } = "LOAN_COLLECTION";

        [Required, Range(1, double.MaxValue)]
        public decimal Amount { get; set; }

        public string? PayerReceiverName { get; set; }
        public string? Description { get; set; }
    }
}
