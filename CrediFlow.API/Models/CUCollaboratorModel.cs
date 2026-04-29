namespace CrediFlow.API.Models
{
    /// <summary>
    /// Model tạo mới và cập nhật cộng tác viên (CTV).
    /// Nếu CollaboratorId == null hoặc Guid.Empty → tạo mới, ngược lại → cập nhật.
    /// </summary>
    public class CUCollaboratorModel
    {
        /// <summary>Id CTV – null khi tạo mới.</summary>
        public Guid? CollaboratorId { get; set; }

        /// <summary>Họ và tên (bắt buộc).</summary>
        public string FullName { get; set; } = null!;

        /// <summary>Số điện thoại.</summary>
        public string? Phone { get; set; }

        /// <summary>Số CMND/CCCD của CTV (để xác định khi thanh toán hoa hồng).</summary>
        public string? IdNumber { get; set; }

        /// <summary>Cửa hàng mà CTV liên kết.</summary>
        public Guid? StoreId { get; set; }

        /// <summary>Ghi chú.</summary>
        public string? Note { get; set; }

        /// <summary>Tỷ lệ hoa hồng (%) áp dụng cho CTV này, ví dụ 2.5 = 2.5%.</summary>
        public decimal? CommissionRate { get; set; }

        /// <summary>Trạng thái hoạt động.</summary>
        public bool IsActive { get; set; } = true;
    }
}
