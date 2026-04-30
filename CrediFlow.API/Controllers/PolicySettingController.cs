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
    public class PolicySettingController : ControllerBase
    {
        private readonly IPolicySettingService _policySettingService;
        private readonly IUserInfoService      _userInfoService;

        public PolicySettingController(IPolicySettingService policySettingService, IUserInfoService userInfoService)
        {
            _policySettingService = policySettingService;
            _userInfoService      = userInfoService;
        }

        // GET api/PolicySetting/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _policySettingService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/PolicySetting/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _policySettingService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy chính sách phí.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/PolicySetting/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchPolicySettingRequest request)
        {
            var rs = await _policySettingService.SearchPolicySetting(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/PolicySetting/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUPolicySettingModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            // StoreManager chỉ được cài đặt chính sách cho chi nhánh mình
            if ((_userInfoService.IsStoreManager || _userInfoService.IsRegionalManager) && !_userInfoService.IsAdmin)
            {
                var allowedStoreIds = _userInfoService.GetStoreScopeIds();
                bool hasOtherStore = model.StoreIds.Any(id => allowedStoreIds is not null && !allowedStoreIds.Contains(id));
                if (hasOtherStore)
                    return Ok(ResultAPI.Error(null, "Bạn không có quyền cập nhật chính sách của chi nhánh khác."));
            }

            bool isUpdate = model.PolicyId.HasValue && model.PolicyId != Guid.Empty;
            try
            {
                var rs = await _policySettingService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} chính sách thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }

        // POST api/PolicySetting/GetActivePolicy
        /// <summary>
        /// Lấy chính sách đang áp dụng cho một cửa hàng tại ngày cụ thể.
        /// Ưu tiên: chính sách riêng cửa hàng → chính sách toàn hệ thống.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetActivePolicy([FromBody] GetActivePolicyRequest request)
        {
            var rs = await _policySettingService.GetActivePolicy(request.StoreId, request.Date);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy chính sách áp dụng.", 404));

            return Ok(ResultAPI.Success(rs));
        }
    }

    public class SearchPolicySettingRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 1000;
        /// <summary>EffectiveFrom | EffectiveTo | CreatedAt</summary>
        public string? SortBy    { get; set; } = "EffectiveFrom";
        public bool    SortDesc  { get; set; } = true;
    }

    public class GetActivePolicyRequest
    {
        public Guid     StoreId { get; set; }
        public DateOnly Date    { get; set; }
    }
}
