using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật chính sách phí.</summary>
    public class CUPolicySettingModel
    {
        /// <summary>Id chính sách – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? PolicyId { get; set; }

        /// <summary>
        /// Danh sách cửa hàng áp dụng.
        /// Rỗng / null = chính sách toàn hệ thống; có giá trị = chính sách riêng cho các cửa hàng đó.
        /// </summary>
        public List<Guid> StoreIds { get; set; } = new();

        [Required]
        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo  { get; set; }

        /// <summary>Tỷ lệ phạt tất toán sớm (%), ví dụ 5.00 = 5%.</summary>
        public decimal EarlySettlementPenaltyRate { get; set; } = 5m;
        public decimal LatePaymentPenaltyRate     { get; set; } = 8m;
        public short   LatePaymentStartDay        { get; set; } = 4;
        public short   BadDebtStartDay            { get; set; } = 11;

        /// <summary>% chiết khấu bảo hiểm công ty nhận từ nhà bảo hiểm (0–100). Chỉ Admin xem.</summary>
        public decimal InsuranceDiscountRate { get; set; } = 0m;

        /// <summary>Số ngày cảnh báo trước khi đến hạn, ví dụ [5, 10, 15].</summary>
        public List<short> WarningDays { get; set; } = new();
    }
}
