using System.ComponentModel.DataAnnotations;

namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật người dùng hệ thống.</summary>
    public class CUAppUserModel
    {
        /// <summary>Id user – null khi tạo mới, có giá trị khi cập nhật.</summary>
        public Guid? UserId { get; set; }

        [Required]
        public string Username { get; set; } = null!;

        /// <summary>Mật khẩu plain-text – bắt buộc khi tạo mới, để trống khi cập nhật nếu không đổi.</summary>
        public string? Password { get; set; }

        [Required]
        public string FullName { get; set; } = null!;

        public string? Phone  { get; set; }
        public string? Email  { get; set; }

        /// <summary>Vai trò: ADMIN | REGIONAL_MANAGER | STORE_MANAGER | STAFF</summary>
        [Required]
        public string RoleCode { get; set; } = null!;

        /// <summary>Chi nhánh (bắt buộc với STORE_MANAGER và STAFF, null với ADMIN và REGIONAL_MANAGER).</summary>
        public Guid? StoreId { get; set; }

        /// <summary>Danh sách chi nhánh quản lý (bắt buộc với REGIONAL_MANAGER, null với các vai trò khác).</summary>
        public List<Guid>? StoreIds { get; set; }

        public bool IsActive           { get; set; } = true;
        public bool MustChangePassword { get; set; } = false;
    }
}
