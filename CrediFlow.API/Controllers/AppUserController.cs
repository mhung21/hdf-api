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
    public class AppUserController : ControllerBase
    {
        private readonly IAppUserService  _appUserService;
        private readonly IUserInfoService _userInfoService;

        public AppUserController(IAppUserService appUserService, IUserInfoService userInfoService)
        {
            _appUserService  = appUserService;
            _userInfoService = userInfoService;
        }

        // GET api/AppUser/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _appUserService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/AppUser/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _appUserService.GetDetailAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy người dùng.", 404));

            rs.PasswordHash = string.Empty;
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/AppUser/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchAppUserRequest request)
        {
            var rs = await _appUserService.SearchAppUser(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/AppUser/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUAppUserModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            // Phân quyền tạo/sửa user theo vai trò
            if (!_userInfoService.IsAdmin)
            {
                bool isRegionalManager = _userInfoService.IsRegionalManager;
                bool isStoreManager    = _userInfoService.IsStoreManager;

                if (!isRegionalManager && !isStoreManager)
                    return Ok(ResultAPI.ResultWithAccessDenined());

                // Không được tạo ADMIN hoặc REGIONAL_MANAGER
                if (model.RoleCode == RoleCode.Admin || model.RoleCode == RoleCode.RegionalManager)
                    return Ok(ResultAPI.ResultWithAccessDenined());

                if (isRegionalManager)
                {
                    // Regional Manager chỉ được tạo user cho các chi nhánh trong phạm vi quản lý
                    if (model.StoreId.HasValue && !_userInfoService.AssignedStoreIds.Contains(model.StoreId.Value))
                        return Ok(ResultAPI.Error(null, "Bạn không có quyền quản lý user ngoài vùng của mình."));
                }
                else
                {
                    // Store Manager chỉ được tạo user trong chi nhánh của mình
                    if (model.StoreId.HasValue && model.StoreId != _userInfoService.StoreId)
                        return Ok(ResultAPI.Error(null, "Bạn không có quyền quản lý user ngoài chi nhánh của mình."));
                }
            }

            bool isUpdate = model.UserId.HasValue && model.UserId != Guid.Empty;
            try
            {
                var rs = await _appUserService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} người dùng {model.FullName} thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (ArgumentException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 400));
            }
            catch (InvalidOperationException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 400));
            }
        }
    }

    public class SearchAppUserRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 1000;
        /// <summary>FullName | Username | RoleCode | IsActive | CreatedAt</summary>
        public string? SortBy    { get; set; } = "FullName";
        public bool    SortDesc  { get; set; } = false;
    }
}
