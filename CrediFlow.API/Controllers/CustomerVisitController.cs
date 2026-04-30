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
    public class CustomerVisitController : ControllerBase
    {
        private readonly ICustomerVisitService _customerVisitService;
        private readonly IUserInfoService      _userInfoService;

        public CustomerVisitController(ICustomerVisitService customerVisitService, IUserInfoService userInfoService)
        {
            _customerVisitService = customerVisitService;
            _userInfoService      = userInfoService;
        }

        // GET api/CustomerVisit/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _customerVisitService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/CustomerVisit/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _customerVisitService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy lượt đến.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/CustomerVisit/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchCustomerVisitRequest request)
        {
            var rs = await _customerVisitService.SearchCustomerVisit(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/CustomerVisit/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUCustomerVisitModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            if ((_userInfoService.IsStoreManager || _userInfoService.IsRegionalManager) && !_userInfoService.IsAdmin)
            {
                if (!_userInfoService.GetStoreScopeIds(model.StoreId).Any())
                    return Ok(ResultAPI.Error(null, "Bạn không có quyền ghi nhận lượt đến cho chi nhánh khác."));
            }

            bool isUpdate = model.VisitId.HasValue && model.VisitId != Guid.Empty;
            try
            {
                var rs = await _customerVisitService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} lượt đến thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }
    }

    public class SearchCustomerVisitRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 1000;
        /// <summary>VisitDate | VisitType | SourceType | CreatedAt</summary>
        public string? SortBy    { get; set; } = "VisitDate";
        public bool    SortDesc  { get; set; } = true;
    }
}
