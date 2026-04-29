namespace CrediFlow.API.Models
{
    /// <summary>
    /// Model dùng cho tạo mới và cập nhật khách hàng.
    /// Nếu CustomerId == null hoặc Guid.Empty → tạo mới, ngược lại → cập nhật.
    /// </summary>
    public class CUCustomerModel
    {
        /// <summary>Id khách hàng – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? CustomerId { get; set; }

        /// <summary>Số CMND / CCCD (bắt buộc).</summary>
        public string NationalId { get; set; } = null!;

        /// <summary>Mã khách hàng (tùy chọn, hệ thống có thể tự sinh).</summary>
        public string? CustomerCode { get; set; }

        /// <summary>Họ và tên (bắt buộc).</summary>
        public string FullName { get; set; } = null!;

        /// <summary>Ngày sinh.</summary>
        public DateOnly? DateOfBirth { get; set; }

        /// <summary>Giới tính (ví dụ: "M" / "F").</summary>
        public string? Gender { get; set; }

        /// <summary>Số điện thoại.</summary>
        public string? Phone { get; set; }

        /// <summary>Địa chỉ.</summary>
        public string? Address { get; set; }

        /// <summary>Nguồn tiếp cận lần đầu. Chỉ chấp nhận: <c>"CTV"</c>, <c>"VANG_LAI"</c> hoặc <c>"KHACH_CU"</c>; để null nếu không xác định.</summary>
        public string? FirstSourceType { get; set; }

        /// <summary>Khách hàng có lịch sử nợ xấu không.</summary>
        public bool HasBadHistory { get; set; }

        /// <summary>Ghi chú về lịch sử nợ xấu.</summary>
        public string? BadHistoryNote { get; set; }

        /// <summary>Chi nhánh tiếp nhận lần đầu.</summary>
        public Guid? FirstStoreId { get; set; }

        /// <summary>
        /// Id CTV giới thiệu – chỉ có giá trị khi <see cref="FirstSourceType"/> == "CTV".
        /// </summary>
        public Guid? ReferredByCollaboratorId { get; set; }
    }
}
