namespace CrediFlow.API.Models
{
    /// <summary>Giới tính khách hàng.</summary>
    public static class Gender
    {
        public const string Male   = "MALE";
        public const string Female = "FEMALE";
        public const string Other  = "OTHER";
    }

    /// <summary>Nguồn tiếp cận.</summary>
    public static class SourceType
    {
        public const string Ctv     = "CTV";
        public const string VangLai = "VANG_LAI";
        public const string KhachCu = "KHACH_CU";
    }

    /// <summary>Loại lượt đến của khách hàng.</summary>
    public static class VisitType
    {
        public const string New = "NEW";
        public const string Old = "OLD";
    }

    /// <summary>Vai trò người dùng.</summary>
    public static class RoleCode
    {
        public const string Admin           = "ADMIN";
        public const string RegionalManager = "REGIONAL_MANAGER";
        public const string StoreManager    = "STORE_MANAGER";
        public const string Staff           = "STAFF";
    }

    /// <summary>Trạng thái hợp đồng vay.</summary>
    /// <remarks>
    /// Luồng duyệt: DRAFT → PENDING_APPROVAL → PENDING_DISBURSEMENT → DISBURSED
    /// </remarks>
    public static class LoanContractStatus
    {
        /// <summary>Đang soạn thảo – nhân viên có thể sửa tự do.</summary>
        public const string Draft              = "DRAFT";
        /// <summary>Đã gửi quản lý, chờ duyệt – không được sửa nội dung.</summary>
        public const string PendingApproval    = "PENDING_APPROVAL";
        /// <summary>Quản lý đã duyệt, chờ giải ngân.</summary>
        public const string PendingDisbursement = "PENDING_DISBURSEMENT";
        /// <summary>Đã giải ngân, đang trong hạn trả nợ.</summary>
        public const string Disbursed          = "DISBURSED";
        public const string Cancelled          = "CANCELLED";
        public const string Settled            = "SETTLED";
        public const string BadDebt            = "BAD_DEBT";
        public const string Closed             = "CLOSED";
        /// <summary>Đã đóng sau quá trình thu hồi nợ xấu.</summary>
        public const string BadDebtClosed      = "BAD_DEBT_CLOSED";
        /// <summary>Nhân viên đã đề xuất chuyển nợ xấu — chờ quản lý duyệt.</summary>
        public const string PendingBadDebt     = "PENDING_BAD_DEBT";
    }

    /// <summary>Loại hợp đồng vay.</summary>
    public static class LoanContractType
    {
        public const string Installment = "INSTALLMENT";
        public const string Pawn        = "PAWN";
    }

    /// <summary>Trạng thái kỳ trả nợ.</summary>
    public static class ScheduleStatus
    {
        public const string Pending  = "PENDING";
        public const string Partial  = "PARTIAL";
        public const string Paid     = "PAID";
        public const string Overdue  = "OVERDUE";
        public const string BadDebt  = "BAD_DEBT";
    }

    /// <summary>Mã loại phí / khoản phí hợp đồng.</summary>
    public static class LoanChargeCode
    {
        public const string FileFee                  = "FILE_FEE";
        public const string Insurance                = "INSURANCE";
        public const string PeriodicFee              = "PERIODIC_FEE";
        public const string LatePenalty              = "LATE_PENALTY";
        public const string EarlySettlementPenalty   = "EARLY_SETTLEMENT_PENALTY";
        public const string OtherIncome              = "OTHER_INCOME";
        public const string OtherExpense             = "OTHER_EXPENSE";
    }

    /// <summary>Trạng thái phí hợp đồng.</summary>
    public static class LoanChargeStatus
    {
        public const string Open      = "OPEN";
        public const string Partial   = "PARTIAL";
        public const string Paid      = "PAID";
        public const string Waived    = "WAIVED";
        public const string Cancelled = "CANCELLED";
    }

    /// <summary>Loại tất toán hợp đồng.</summary>
    public static class SettlementType
    {
        public const string OnTime = "ONTIME";
        public const string Early  = "EARLY";
    }

    /// <summary>Trạng thái hợp đồng bảo hiểm.</summary>
    public static class InsuranceContractStatus
    {
        public const string Active    = "ACTIVE";
        public const string Expired   = "EXPIRED";
        public const string Cancelled = "CANCELLED";
    }

    /// <summary>Loại phiếu thu/chi.</summary>
    public static class VoucherType
    {
        public const string Receipt = "RECEIPT";
        public const string Payment = "PAYMENT";
    }

    /// <summary>Lý do phiếu thu/chi.</summary>
    public static class VoucherReasonCode
    {
        public const string InstallmentCollection = "INSTALLMENT_COLLECTION";
        public const string EarlySettlement       = "EARLY_SETTLEMENT";
        public const string UpfrontFeeCollection  = "UPFRONT_FEE_COLLECTION";
        public const string InsuranceCollection   = "INSURANCE_COLLECTION";
        public const string AdjustmentReceipt     = "ADJUSTMENT_RECEIPT";
        public const string AdjustmentPayment     = "ADJUSTMENT_PAYMENT";
        public const string OverpaymentRefund     = "OVERPAYMENT_REFUND";
        public const string OtherIncome           = "OTHER_INCOME";
        public const string OtherExpense          = "OTHER_EXPENSE";
        public const string BadDebtTransfer       = "BAD_DEBT_TRANSFER";
        public const string BadDebtRecovery       = "BAD_DEBT_RECOVERY";
    }

    /// <summary>Trạng thái hồ sơ nợ xấu.</summary>
    public static class BadDebtCaseStatus
    {
        public const string Open       = "OPEN";
        public const string Recovering = "RECOVERING";
        public const string Closed     = "CLOSED";
    }

    /// <summary>Mã phân bổ thành phần phiếu thu/chi.</summary>
    public static class VoucherComponentCode
    {
        public const string Principal                = "PRINCIPAL";
        public const string Interest                 = "INTEREST";
        public const string PeriodicFee              = "PERIODIC_FEE";
        public const string FileFee                  = "FILE_FEE";
        public const string Insurance                = "INSURANCE";
        public const string LatePenalty              = "LATE_PENALTY";
        public const string EarlySettlementPenalty   = "EARLY_SETTLEMENT_PENALTY";
        public const string OtherIncome              = "OTHER_INCOME";
        public const string OtherExpense             = "OTHER_EXPENSE";
    }

    /// <summary>Tên chi nhánh hệ thống.</summary>
    public static class StoreName
    {
        /// <summary>Chi nhánh gốc – gán tự động cho ADMIN / REGIONAL_MANAGER.</summary>
        public const string Headquarters = "Tổng công ty";
    }
}
