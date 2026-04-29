using CrediFlow.API.Models;
using CrediFlow.API.Services;
using CrediFlow.Common.Models;
using CrediFlow.Common.Services;
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

        public RolePermissionController(IRolePermissionService rolePermissionService, IUserInfoService userInfoService)
        {
            _rolePermissionService = rolePermissionService;
            _userInfoService = userInfoService;
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
    }
}
