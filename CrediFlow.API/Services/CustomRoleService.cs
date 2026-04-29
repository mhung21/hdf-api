using CrediFlow.API.Models;
using CrediFlow.API.Utils;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace CrediFlow.API.Services
{
    public interface ICustomRoleService : IBaseService<CustomRole>
    {
        /// <summary>Lấy tất cả vai trò tùy chỉnh (Admin: tất cả; StoreManager: global + của store mình).</summary>
        Task<IList<CustomRoleDto>> GetAll();

        /// <summary>Lấy chi tiết một vai trò tùy chỉnh theo ID.</summary>
        Task<CustomRoleDto?> GetById(Guid customRoleId);

        /// <summary>Tạo mới hoặc cập nhật vai trò tùy chỉnh.</summary>
        Task<CustomRole> Save(CUCustomRoleModel model);

        /// <summary>Xóa mềm vai trò tùy chỉnh (đặt is_active = false).</summary>
        Task Delete(Guid customRoleId);

        /// <summary>Lấy danh sách quyền đã gán cho vai trò tùy chỉnh.</summary>
        Task<IList<PermissionDto>> GetPermissions(Guid customRoleId);

        /// <summary>Cập nhật toàn bộ danh sách quyền cho vai trò tùy chỉnh.</summary>
        Task SavePermissions(Guid customRoleId, IList<Guid> permissionIds);

        /// <summary>Lấy danh sách người dùng đang được gán vai trò này.</summary>
        Task<IList<UserInRoleDto>> GetUsersWithRole(Guid customRoleId);

        /// <summary>Cập nhật danh sách vai trò tùy chỉnh cho một người dùng.</summary>
        Task AssignRolesToUser(Guid userId, IList<Guid> customRoleIds);

        /// <summary>Lấy danh sách vai trò tùy chỉnh của một người dùng.</summary>
        Task<IList<CustomRoleDto>> GetRolesForUser(Guid userId);
    }

    public class CustomRoleService : BaseService<CustomRole, CrediflowContext>, ICustomRoleService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomRoleService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user,
            IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
            : base(dbContext, cachingHelper, user)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IList<CustomRoleDto>> GetAll()
        {
            IQueryable<CustomRole> query = DbContext.CustomRoles;

            // StoreManager chỉ thấy vai trò global (null storeId) + vai trò của store mình
            if (!User.IsAdmin && User.IsStoreManager)
            {
                var storeId = User.StoreId;
                query = query.Where(r => r.StoreId == null || r.StoreId == storeId);
            }

            return await query
                .OrderBy(r => r.StoreId == null ? 0 : 1)
                .ThenBy(r => r.RoleName)
                .Select(r => new CustomRoleDto
                {
                    CustomRoleId = r.CustomRoleId,
                    RoleName = r.RoleName,
                    Description = r.Description,
                    IsActive = r.IsActive,
                    StoreId = r.StoreId,
                    PermissionCount = r.CustomRolePermissions.Count,
                    UserCount = r.UserCustomRoles.Count,
                    CreatedAt = r.CreatedAt,
                })
                .ToListAsync();
        }

        public async Task<CustomRoleDto?> GetById(Guid customRoleId)
        {
            return await DbContext.CustomRoles
                .Where(r => r.CustomRoleId == customRoleId)
                .Select(r => new CustomRoleDto
                {
                    CustomRoleId = r.CustomRoleId,
                    RoleName = r.RoleName,
                    Description = r.Description,
                    IsActive = r.IsActive,
                    StoreId = r.StoreId,
                    PermissionCount = r.CustomRolePermissions.Count,
                    UserCount = r.UserCustomRoles.Count,
                    CreatedAt = r.CreatedAt,
                })
                .FirstOrDefaultAsync();
        }

        public async Task<CustomRole> Save(CUCustomRoleModel model)
        {
            // Xác định storeId cho vai trò này
            Guid? targetStoreId;
            if (User.IsAdmin)
            {
                // Admin có thể tạo vai trò global (null) hoặc chỉ định store cụ thể
                targetStoreId = model.StoreId;
            }
            else if (User.IsStoreManager)
            {
                // StoreManager chỉ tạo được vai trò thuộc store của mình
                targetStoreId = User.StoreId;
            }
            else
            {
                throw new UnauthorizedAccessException("Bạn không có quyền quản lý vai trò.");
            }

            // Kiểm tra trùng tên trong cùng phạm vi (store hoặc global)
            var duplicate = await DbContext.CustomRoles
                .AnyAsync(r => r.RoleName == model.RoleName
                    && r.StoreId == targetStoreId
                    && (model.CustomRoleId == null || r.CustomRoleId != model.CustomRoleId));
            if (duplicate)
                throw new InvalidOperationException($"Đã tồn tại vai trò với tên \"{model.RoleName}\" trong phạm vi này.");

            if (model.CustomRoleId == null || model.CustomRoleId == Guid.Empty)
            {
                // Tạo mới
                var entity = new CustomRole
                {
                    CustomRoleId = Guid.CreateVersion7(),
                    RoleName = model.RoleName.Trim(),
                    Description = model.Description?.Trim(),
                    IsActive = true,
                    StoreId = targetStoreId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.UserId,
                };
                DbContext.CustomRoles.Add(entity);
                await DbContext.SaveChangesAsync();
                return entity;
            }
            else
            {
                var entity = await DbContext.CustomRoles.FindAsync(model.CustomRoleId)
                    ?? throw new KeyNotFoundException("Không tìm thấy vai trò.");

                // StoreManager chỉ được sửa vai trò của store mình
                if (!User.IsAdmin && entity.StoreId != User.StoreId)
                    throw new UnauthorizedAccessException("Bạn chỉ có thể chỉnh sửa vai trò của chi nhánh mình.");

                entity.RoleName = model.RoleName.Trim();
                entity.Description = model.Description?.Trim();
                entity.IsActive = model.IsActive;
                entity.UpdatedAt = DateTime.Now;
                entity.UpdatedBy = User.UserId;
                await DbContext.SaveChangesAsync();
                return entity;
            }
        }

        public async Task Delete(Guid customRoleId)
        {
            var entity = await DbContext.CustomRoles.FindAsync(customRoleId)
                ?? throw new KeyNotFoundException("Không tìm thấy vai trò.");

            // StoreManager chỉ được xóa vai trò của store mình
            if (!User.IsAdmin && entity.StoreId != User.StoreId)
                throw new UnauthorizedAccessException("Bạn chỉ có thể vô hiệu hóa vai trò của chi nhánh mình.");

            entity.IsActive = false;
            entity.UpdatedAt = DateTime.Now;
            entity.UpdatedBy = User.UserId;
            await DbContext.SaveChangesAsync();
        }

        public async Task<IList<PermissionDto>> GetPermissions(Guid customRoleId)
        {
            _ = await DbContext.CustomRoles.FindAsync(customRoleId)
                ?? throw new KeyNotFoundException("Không tìm thấy vai trò.");

            return await DbContext.CustomRolePermissions
                .Where(crp => crp.CustomRoleId == customRoleId && crp.Permission.IsActive)
                .Select(crp => new PermissionDto
                {
                    PermissionId = crp.Permission.PermissionId,
                    PermissionCode = crp.Permission.PermissionCode,
                    PermissionName = crp.Permission.PermissionName,
                    Resource = crp.Permission.Resource,
                    Action = crp.Permission.Action,
                    Description = crp.Permission.Description,
                    IsDelegatable = crp.Permission.IsDelegatable,
                })
                .OrderBy(p => p.Resource).ThenBy(p => p.Action)
                .ToListAsync();
        }

        public async Task SavePermissions(Guid customRoleId, IList<Guid> permissionIds)
        {
            var role = await DbContext.CustomRoles.FindAsync(customRoleId)
                ?? throw new KeyNotFoundException("Không tìm thấy vai trò.");

            // StoreManager chỉ được set quyền cho vai trò của store mình
            if (!User.IsAdmin && role.StoreId != User.StoreId)
                throw new UnauthorizedAccessException("Bạn chỉ có thể quản lý quyền của vai trò trong chi nhánh mình.");

            // Tất cả quyền được gán phải có is_delegatable = true
            if (permissionIds.Count > 0)
            {
                var nonDelegatableCount = await DbContext.Permissions
                    .Where(p => permissionIds.Contains(p.PermissionId) && !p.IsDelegatable)
                    .CountAsync();
                if (nonDelegatableCount > 0)
                    throw new InvalidOperationException("Một số quyền không thể được gán vào vai trò tùy chỉnh (không phải delegatable).");
            }

            // StoreManager chỉ được gán các quyền trong phạm vi của STORE_MANAGER (không được gán quyền vượt cấp)
            if (!User.IsAdmin && User.IsStoreManager && permissionIds.Count > 0)
            {
                var smPermIds = await DbContext.RolePermissions
                    .Where(rp => rp.RoleCode == "STORE_MANAGER")
                    .Select(rp => rp.PermissionId)
                    .ToListAsync();
                var smPermIdSet = new HashSet<Guid>(smPermIds);
                var forbidden = permissionIds.Where(id => !smPermIdSet.Contains(id)).ToList();
                if (forbidden.Count > 0)
                    throw new InvalidOperationException("Một số quyền vượt quá phạm vi Quản lý chi nhánh và không thể gán.");
            }

            // Xóa toàn bộ quyền hiện tại rồi thêm lại (replace all strategy)
            var existing = await DbContext.CustomRolePermissions
                .Where(crp => crp.CustomRoleId == customRoleId)
                .ToListAsync();
            DbContext.CustomRolePermissions.RemoveRange(existing);

            foreach (var permId in permissionIds.Distinct())
            {
                DbContext.CustomRolePermissions.Add(new CustomRolePermission
                {
                    CustomRolePermissionId = Guid.CreateVersion7(),
                    CustomRoleId = customRoleId,
                    PermissionId = permId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.UserId,
                });
            }

            await DbContext.SaveChangesAsync();
        }

        public async Task<IList<UserInRoleDto>> GetUsersWithRole(Guid customRoleId)
        {
            return await DbContext.UserCustomRoles
                .Where(ucr => ucr.CustomRoleId == customRoleId)
                .Select(ucr => new UserInRoleDto
                {
                    UserId = ucr.UserId,
                    Username = ucr.User.Username,
                    FullName = ucr.User.FullName,
                    RoleCode = ucr.User.RoleCode,
                    AssignedAt = ucr.CreatedAt,
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        public async Task AssignRolesToUser(Guid userId, IList<Guid> customRoleIds)
        {
            // StoreManager chỉ được gán vai trò của store mình cho user trong store mình
            if (!User.IsAdmin && User.IsStoreManager)
            {
                var targetUser = await DbContext.AppUsers.FindAsync(userId)
                    ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");
                if (targetUser.StoreId != User.StoreId)
                    throw new UnauthorizedAccessException("Bạn chỉ có thể thay đổi vai trò cho nhân viên trong chi nhánh mình.");

                // Chỉ được gán vai trò global (storeId=null) hoặc của store mình
                var storeId = User.StoreId;
                var allowedRoleIds = await DbContext.CustomRoles
                    .Where(r => r.IsActive && (r.StoreId == null || r.StoreId == storeId))
                    .Select(r => r.CustomRoleId)
                    .ToListAsync();
                var allowedSet = new HashSet<Guid>(allowedRoleIds);
                var forbidden = customRoleIds.Where(id => !allowedSet.Contains(id)).ToList();
                if (forbidden.Count > 0)
                    throw new UnauthorizedAccessException("Một số vai trò không thuộc phạm vi chi nhánh của bạn.");
            }

            // Xóa toàn bộ custom roles hiện tại của user rồi set lại
            var existing = await DbContext.UserCustomRoles
                .Where(ucr => ucr.UserId == userId)
                .ToListAsync();
            DbContext.UserCustomRoles.RemoveRange(existing);

            foreach (var roleId in customRoleIds.Distinct())
            {
                DbContext.UserCustomRoles.Add(new UserCustomRole
                {
                    UserCustomRoleId = Guid.CreateVersion7(),
                    UserId = userId,
                    CustomRoleId = roleId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.UserId,
                });
            }

            await DbContext.SaveChangesAsync();

            // Xóa cache quyền ngay lập tức — hiệu lực tức thì trên API
            PermissionChecker.InvalidatePermissionCache(CachingHelper, userId);

            // Revoke tất cả sessions hiện tại của user — buộc đăng nhập lại để JWT mới
            // phản ánh đúng quyền sau khi thay đổi custom roles
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(Config.Urls.IdentityServer);
                var incomingToken = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(incomingToken))
                    client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(incomingToken);
                await client.PostAsJsonAsync($"/api/auth/revoke-user-sessions/{userId}", new { reason = "Custom roles updated" });
            }
            catch
            {
                // Không fail toàn bộ operation nếu revoke call lỗi (network, timeout)
                // User sẽ tự timeout theo access token TTL
            }
        }

        public async Task<IList<CustomRoleDto>> GetRolesForUser(Guid userId)
        {
            return await DbContext.UserCustomRoles
                .Where(ucr => ucr.UserId == userId && ucr.CustomRole.IsActive)
                .Select(ucr => new CustomRoleDto
                {
                    CustomRoleId = ucr.CustomRole.CustomRoleId,
                    RoleName = ucr.CustomRole.RoleName,
                    Description = ucr.CustomRole.Description,
                    IsActive = ucr.CustomRole.IsActive,
                    StoreId = ucr.CustomRole.StoreId,
                    PermissionCount = ucr.CustomRole.CustomRolePermissions.Count,
                    UserCount = ucr.CustomRole.UserCustomRoles.Count,
                    CreatedAt = ucr.CustomRole.CreatedAt,
                })
                .OrderBy(r => r.RoleName)
                .ToListAsync();
        }
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────
    public class CustomRoleDto
    {
        public Guid CustomRoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        /// <summary>null = global (Admin tạo); có giá trị = chỉ trong store cụ thể.</summary>
        public Guid? StoreId { get; set; }
        public int PermissionCount { get; set; }
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserInRoleDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string RoleCode { get; set; } = null!;
        public DateTime AssignedAt { get; set; }
    }
}
