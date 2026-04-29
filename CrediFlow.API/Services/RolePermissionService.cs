using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface IRolePermissionService : IBaseService<RolePermission>
    {
        /// <summary>Lấy danh sách quyền mặc định của một vai trò.</summary>
        Task<IList<PermissionDto>> GetPermissionsByRole(string roleCode);

        /// <summary>Lấy toàn bộ danh sách quyền đang active trong hệ thống.</summary>
        Task<IList<PermissionDto>> GetAllPermissions();

        /// <summary>
        /// Lấy danh sách quyền có thể gán cho vai trò tùy chỉnh — đã loại bỏ quyền mặc định của STAFF.
        /// Admin: tất cả quyền trừ STAFF defaults.
        /// StoreManager: quyền của STORE_MANAGER trừ STAFF defaults.
        /// </summary>
        Task<IList<PermissionDto>> GetAssignablePermissions();
    }

    public class RolePermissionService : BaseService<RolePermission, CrediflowContext>, IRolePermissionService
    {
        public RolePermissionService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        /// <summary>Lấy danh sách quyền mặc định của một vai trò.</summary>
        public async Task<IList<PermissionDto>> GetPermissionsByRole(string roleCode)
        {
            var permissions = await DbContext.RolePermissions
                .Where(rp => rp.RoleCode == roleCode && rp.Permission.IsActive)
                .Select(rp => new PermissionDto
                {
                    PermissionId = rp.Permission.PermissionId,
                    PermissionCode = rp.Permission.PermissionCode,
                    PermissionName = rp.Permission.PermissionName,
                    Resource = rp.Permission.Resource,
                    Action = rp.Permission.Action,
                    Description = rp.Permission.Description
                })
                .OrderBy(p => p.Resource).ThenBy(p => p.Action)
                .ToListAsync();

            return permissions;
        }

        /// <summary>Lấy toàn bộ danh sách quyền đang active trong hệ thống.</summary>
        public async Task<IList<PermissionDto>> GetAllPermissions()
        {
            return await DbContext.Permissions
                .Where(p => p.IsActive)
                .Select(p => new PermissionDto
                {
                    PermissionId = p.PermissionId,
                    PermissionCode = p.PermissionCode,
                    PermissionName = p.PermissionName,
                    Resource = p.Resource,
                    Action = p.Action,
                    Description = p.Description,
                })
                .OrderBy(p => p.Resource).ThenBy(p => p.Action)
                .ToListAsync();
        }

        /// <summary>
        /// Quyền có thể gán cho custom role — loại bỏ những quyền STAFF đã có mặc định.
        /// Admin thấy tất cả quyền trừ STAFF defaults.
        /// StoreManager thấy quyền trong phạm vi STORE_MANAGER trừ STAFF defaults.
        /// </summary>
        public async Task<IList<PermissionDto>> GetAssignablePermissions()
        {
            // Lấy permission codes của STAFF (quyền mặc định, không cần trao thêm)
            var staffCodes = await DbContext.RolePermissions
                .Where(rp => rp.RoleCode == "STAFF")
                .Select(rp => rp.Permission.PermissionCode)
                .ToListAsync();

            if (User.IsAdmin)
            {
                return await DbContext.Permissions
                    .Where(p => p.IsActive && p.IsDelegatable && !staffCodes.Contains(p.PermissionCode))
                    .Select(p => new PermissionDto
                    {
                        PermissionId = p.PermissionId,
                        PermissionCode = p.PermissionCode,
                        PermissionName = p.PermissionName,
                        Resource = p.Resource,
                        Action = p.Action,
                        Description = p.Description,
                        IsDelegatable = p.IsDelegatable,
                    })
                    .OrderBy(p => p.Resource).ThenBy(p => p.Action)
                    .ToListAsync();
            }

            if (User.IsStoreManager)
            {
                // Chỉ những quyền trong tập STORE_MANAGER, là delegatable, và STAFF chưa có mặc định
                var smCodes = await DbContext.RolePermissions
                    .Where(rp => rp.RoleCode == "STORE_MANAGER")
                    .Select(rp => rp.Permission.PermissionCode)
                    .ToListAsync();

                return await DbContext.Permissions
                    .Where(p => p.IsActive
                        && p.IsDelegatable
                        && smCodes.Contains(p.PermissionCode)
                        && !staffCodes.Contains(p.PermissionCode))
                    .Select(p => new PermissionDto
                    {
                        PermissionId = p.PermissionId,
                        PermissionCode = p.PermissionCode,
                        PermissionName = p.PermissionName,
                        Resource = p.Resource,
                        Action = p.Action,
                        Description = p.Description,
                        IsDelegatable = p.IsDelegatable,
                    })
                    .OrderBy(p => p.Resource).ThenBy(p => p.Action)
                    .ToListAsync();
            }

            return new List<PermissionDto>();
        }
    }

    /// <summary>DTO cho permission hiển thị.</summary>
    public class PermissionDto
    {
        public Guid PermissionId { get; set; }
        public string PermissionCode { get; set; } = null!;
        public string PermissionName { get; set; } = null!;
        public string Resource { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsDelegatable { get; set; }
    }
}
