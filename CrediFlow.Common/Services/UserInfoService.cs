using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CrediFlow.Common.Services
{
    public interface IUserInfoService
    {
        Guid UserId { get; }
        string UserName { get; }
        string DisplayName { get; }
        string Email { get; }
        Guid? StoreId { get; }
        bool IsAdmin { get; }
        bool IsStoreManager { get; }
        bool IsRegionalManager { get; }
        bool IsActive { get; }
        string RoleCode { get; }
        IReadOnlyList<string> Permissions { get; }
        /// <summary>Danh sách chi nhánh mà Quản lý vùng được phân công (rỗng với vai trò khác).</summary>
        IReadOnlyList<Guid> AssignedStoreIds { get; }
        /// <summary>Danh sách chi nhánh user được phép truy cập. Null = toàn bộ chi nhánh (Admin, không lọc).</summary>
        List<Guid>? GetStoreScopeIds(Guid? requestedStoreId = null);
    }

    public class UserInfoService : IUserInfoService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserInfoService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No user context available");

        public Guid UserId
        {
            get
            {
                var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
                    throw new InvalidOperationException("User ID not found in claims");

                return userId;
            }
        }

        public string UserName =>
            User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.Identity?.Name
            ?? throw new InvalidOperationException("Username not found in claims");

        public string DisplayName =>
            User.FindFirst("full_name")?.Value ?? UserName;

        public string Email =>
            User.FindFirst("email")?.Value ?? string.Empty;

        public Guid? StoreId
        {
            get
            {
                var v = User.FindFirst("store_id")?.Value;
                return string.IsNullOrEmpty(v) ? null : Guid.Parse(v);
            }
        }

        public bool IsAdmin =>
            string.Equals(User.FindFirst("is_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        public bool IsStoreManager =>
            string.Equals(User.FindFirst("is_store_manager")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        public bool IsRegionalManager =>
            string.Equals(User.FindFirst("is_regional_manager")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        public bool IsActive =>
            string.Equals(User.FindFirst("is_active")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        public string RoleCode =>
            User.FindFirst("role_code")?.Value
            ?? User.FindFirst(ClaimTypes.Role)?.Value
            ?? throw new InvalidOperationException("Role not found in claims");

        public IReadOnlyList<string> Permissions =>
            User.FindAll("permission").Select(c => c.Value).ToList();

        public IReadOnlyList<Guid> AssignedStoreIds =>
            User.FindAll("assigned_store_id")
                .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();

        public List<Guid>? GetStoreScopeIds(Guid? requestedStoreId = null)
        {
            if (IsAdmin)
            {
                return requestedStoreId.HasValue ? new List<Guid> { requestedStoreId.Value } : null;
            }

            var scopeIds = IsRegionalManager
                ? AssignedStoreIds.ToList()
                : StoreId.HasValue ? new List<Guid> { StoreId.Value } : new List<Guid>();

            if (requestedStoreId.HasValue)
            {
                return scopeIds.Contains(requestedStoreId.Value) ? new List<Guid> { requestedStoreId.Value } : new List<Guid>();
            }

            return scopeIds;
        }
    }
}
