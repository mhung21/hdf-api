namespace CrediFlow.API.Models
{
    /// <summary>Model tạo mới / cập nhật vai trò tùy chỉnh.</summary>
    public class CUCustomRoleModel
    {
        public Guid? CustomRoleId { get; set; }

        public string RoleName { get; set; } = null!;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Khi StoreManager tạo vai trò, backend sẽ tự gán storeId từ context. Field này chỉ Admin dùng để chỉ định rõ store_id (null = global).</summary>
        public Guid? StoreId { get; set; }
    }
}
