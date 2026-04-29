using CrediFlow.Common.Caching;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Utils;

/// <summary>
/// Kiểm tra quyền của user từ DB (với Redis cache) thay vì từ JWT claims.
/// Đảm bảo quyền cập nhật ngay khi admin thay đổi custom roles — không bị delay bởi JWT TTL.
/// </summary>
public static class PermissionChecker
{
    private const int CacheTtlMinutes = 10;

    /// <summary>Kiểm tra user có quyền <paramref name="permissionCode"/> không — tra DB, cache Redis 10 phút.</summary>
    public static async Task<bool> HasPermissionAsync(
        CrediflowContext db, ICachingHelper cache, Guid userId, string roleCode, string permissionCode)
    {
        var perms = await GetPermissionsAsync(db, cache, userId, roleCode);
        return perms.Contains(permissionCode);
    }

    /// <summary>Xóa cache quyền của user — gọi ngay sau khi thay đổi custom roles hoặc user_permissions.</summary>
    public static void InvalidatePermissionCache(ICachingHelper cache, Guid userId)
    {
        cache.Remove($"user_perms:{userId}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<HashSet<string>> GetPermissionsAsync(
        CrediflowContext db, ICachingHelper cache, Guid userId, string roleCode)
    {
        var cacheKey = $"user_perms:{userId}";
        var cached = cache.Get<List<string>>(cacheKey);
        if (cached != null)
            return new HashSet<string>(cached);

        // 1. Quyền theo vai trò gốc (role_permissions)
        var rolePerms = await db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleCode == roleCode)
            .Join(
                db.Permissions.Where(p => p.IsActive),
                rp => rp.PermissionId,
                p  => p.PermissionId,
                (_, p) => p.PermissionCode)
            .ToListAsync();

        // 2. Quyền từ custom roles được gán (user_custom_roles → custom_role_permissions)
        var customPerms = await (
            from ucr in db.UserCustomRoles  where ucr.UserId == userId
            join cr  in db.CustomRoles          on ucr.CustomRoleId equals cr.CustomRoleId
            where cr.IsActive
            join crp in db.CustomRolePermissions on cr.CustomRoleId  equals crp.CustomRoleId
            join p   in db.Permissions           on crp.PermissionId equals p.PermissionId
            where p.IsActive
            select p.PermissionCode
        ).Distinct().ToListAsync();

        // 3. Ghi đè per-user (user_permissions: có thể cấp thêm hoặc thu hồi)
        var overrides = await db.UserPermissions
            .AsNoTracking()
            .Where(up => up.UserId == userId)
            .Join(
                db.Permissions.Where(p => p.IsActive),
                up => up.PermissionId,
                p  => p.PermissionId,
                (up, p) => new { p.PermissionCode, up.IsGranted })
            .ToListAsync();

        var merged = new HashSet<string>(rolePerms);
        merged.UnionWith(customPerms);
        foreach (var ovr in overrides)
        {
            if (ovr.IsGranted) merged.Add(ovr.PermissionCode);
            else               merged.Remove(ovr.PermissionCode);
        }

        cache.Set(cacheKey, merged.ToList(), TimeSpan.FromMinutes(CacheTtlMinutes));
        return merged;
    }
}
