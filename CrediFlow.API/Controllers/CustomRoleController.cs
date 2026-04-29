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
    public class CustomRoleController : ControllerBase
    {
        private readonly ICustomRoleService _customRoleService;
        private readonly IUserInfoService _userInfoService;

        public CustomRoleController(ICustomRoleService customRoleService, IUserInfoService userInfoService)
        {
            _customRoleService = customRoleService;
            _userInfoService = userInfoService;
        }

        /// <summary>Lấy toàn bộ danh sách vai trò tùy chỉnh.</summary>
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            try
            {
                var roles = await _customRoleService.GetAll();
                return Ok(ResultAPI.Success(roles));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>Lấy chi tiết vai trò tùy chỉnh theo ID.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            try
            {
                var role = await _customRoleService.GetById(id);
                if (role == null) return Ok(ResultAPI.ResultWithNotFound());
                return Ok(ResultAPI.Success(role));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>Tạo mới hoặc cập nhật vai trò tùy chỉnh.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUCustomRoleModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(null, "Dữ liệu không hợp lệ", 400));

            if (string.IsNullOrWhiteSpace(model.RoleName))
                return Ok(ResultAPI.Error(null, "Tên vai trò không được để trống", 400));

            // Admin và StoreManager đều được quản lý vai trò (StoreManager chỉ tạo role trong store của mình)
            if (!_userInfoService.IsAdmin && !_userInfoService.IsStoreManager)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                var result = await _customRoleService.Save(model);
                return Ok(ResultAPI.Success(result));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (InvalidOperationException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 400));
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

        /// <summary>Vô hiệu hóa vai trò tùy chỉnh.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Delete([FromBody] Guid id)
        {
            if (!_userInfoService.IsAdmin && !_userInfoService.IsStoreManager)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                await _customRoleService.Delete(id);
                return Ok(ResultAPI.Success(null, "Đã vô hiệu hóa vai trò"));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
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

        /// <summary>Lấy danh sách quyền của vai trò tùy chỉnh.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetPermissions([FromBody] Guid customRoleId)
        {
            try
            {
                var permissions = await _customRoleService.GetPermissions(customRoleId);
                return Ok(ResultAPI.Success(permissions));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>Cập nhật toàn bộ quyền cho vai trò tùy chỉnh (replace all).</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> SavePermissions([FromBody] SaveCustomRolePermissionsRequest request)
        {
            if (!_userInfoService.IsAdmin && !_userInfoService.IsStoreManager)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                await _customRoleService.SavePermissions(request.CustomRoleId, request.PermissionIds);
                return Ok(ResultAPI.Success(null, "Đã lưu danh sách quyền"));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>Cập nhật danh sách vai trò tùy chỉnh cho người dùng.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> AssignRolesToUser([FromBody] AssignCustomRolesToUserRequest request)
        {
            if (!_userInfoService.IsAdmin && !_userInfoService.IsStoreManager)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                await _customRoleService.AssignRolesToUser(request.UserId, request.CustomRoleIds);
                return Ok(ResultAPI.Success(null, "Đã gán vai trò cho người dùng"));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }

        /// <summary>Lấy danh sách vai trò tùy chỉnh của một người dùng.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetRolesForUser([FromBody] Guid userId)
        {
            try
            {
                var roles = await _customRoleService.GetRolesForUser(userId);
                return Ok(ResultAPI.Success(roles));
            }
            catch (Exception ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 500));
            }
        }
    }

    // ─── Request models ────────────────────────────────────────────────────
    public class SaveCustomRolePermissionsRequest
    {
        public Guid CustomRoleId { get; set; }
        public IList<Guid> PermissionIds { get; set; } = new List<Guid>();
    }

    public class AssignCustomRolesToUserRequest
    {
        public Guid UserId { get; set; }
        public IList<Guid> CustomRoleIds { get; set; } = new List<Guid>();
    }
}
