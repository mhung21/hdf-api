using CrediFlow.API.Models;
using CrediFlow.API.Services;
using CrediFlow.API.Utils;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Models;
using CrediFlow.Common.Services;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrediFlow.API.Controllers
{
    [Authorize]
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class RolePermissionController : ControllerBase
    {
        private readonly IRolePermissionService _rolePermissionService;
        private readonly IUserInfoService _userInfoService;
        private readonly CrediflowContext _dbContext;
        private readonly ICachingHelper _cachingHelper;

        public RolePermissionController(
            IRolePermissionService rolePermissionService,
            IUserInfoService userInfoService,
            CrediflowContext dbContext,
            ICachingHelper cachingHelper)
        {
            _rolePermissionService = rolePermissionService;
            _userInfoService = userInfoService;
            _dbContext = dbContext;
            _cachingHelper = cachingHelper;
        }

        /// <summary>
        /// Lấy danh sách quyền mặc định của một vai trò.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetPermissionsByRole([FromBody] string roleCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode))
                return Ok(ResultAPI.Error(null, "Không được để trống vai trò", 400));

            try
            {
                var permissions = await _rolePermissionService.GetPermissionsByRole(roleCode);
                return Ok(ResultAPI.Success(permissions));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>
        /// Lấy toàn bộ danh sách quyền đang active trong hệ thống.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAllPermissions()
        {
            try
            {
                var permissions = await _rolePermissionService.GetAllPermissions();
                return Ok(ResultAPI.Success(permissions));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>
        /// Lấy danh sách quyền có thể gán cho vai trò tùy chỉnh (đã loại trừ quyền mặc định của STAFF).
        /// Admin thấy tất cả quyền trừ STAFF defaults; StoreManager thấy quyền trong phạm vi của mình trừ STAFF defaults.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAssignablePermissions()
        {
            try
            {
                var permissions = await _rolePermissionService.GetAssignablePermissions();
                return Ok(ResultAPI.Success(permissions));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>
        /// Ghi đè toàn bộ quyền mặc định của một vai trò. Chỉ ADMIN.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> SaveRolePermissions([FromBody] SaveRolePermissionsRequest request)
        {
            try
            {
                var result = await _rolePermissionService.SaveRolePermissions(request.RoleCode, request.PermissionIds);
                return Ok(ResultAPI.Success(result, "Cập nhật quyền thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 403));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>
        /// Xóa toàn bộ cache phân quyền của tất cả user. Chỉ ADMIN.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> ClearPermissionCache()
        {
            try
            {
                await PermissionChecker.InvalidateAllPermissionCachesAsync(_dbContext, _cachingHelper);
                return Ok(ResultAPI.Success(null, "Đã xóa cache phân quyền toàn hệ thống"));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }
    }

    public class SaveRolePermissionsRequest
    {
        public string RoleCode { get; set; } = null!;
        public List<Guid> PermissionIds { get; set; } = new();
    }
}
