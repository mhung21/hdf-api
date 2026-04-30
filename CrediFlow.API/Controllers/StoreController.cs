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
    public class StoreController : ControllerBase
    {
        private readonly IStoreService    _storeService;
        private readonly IUserInfoService _userInfoService;

        public StoreController(IStoreService storeService, IUserInfoService userInfoService)
        {
            _storeService    = storeService;
            _userInfoService = userInfoService;
        }

        // GET api/Store/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _storeService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Store/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _storeService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy chi nhánh.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Store/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchStoreRequest request)
        {
            var rs = await _storeService.SearchStore(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Store/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUStoreModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            // Chỉ Admin mới được tạo / cập nhật chi nhánh
            if (!_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            bool isUpdate = model.StoreId.HasValue && model.StoreId != Guid.Empty;
            try
            {
                var rs = await _storeService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} chi nhánh {model.StoreName} thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }

        // POST api/Store/LockDay
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> LockDay([FromBody] StoreDayLockRequest request)
        {
            // Chỉ StoreManager (cửa hàng mình) và Admin mới được khóa ngày
            if (!_userInfoService.IsAdmin && !_userInfoService.IsStoreManager)
                return Ok(ResultAPI.ResultWithAccessDenined());

            if ((_userInfoService.IsStoreManager || _userInfoService.IsRegionalManager) && !_userInfoService.IsAdmin &&
                !_userInfoService.GetStoreScopeIds(request.StoreId).Any())
                return Ok(ResultAPI.Error(null, "Bạn không có quyền khóa ngày của chi nhánh khác."));

            try
            {
                var rs = await _storeService.LockDay(request.StoreId, request.BusinessDate, request.Note);
                return Ok(ResultAPI.Success(rs, $"Đã khóa ngày {request.BusinessDate:dd/MM/yyyy} thành công."));
            }
            catch (KeyNotFoundException ex) { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
        }

        // POST api/Store/UnlockDay
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> UnlockDay([FromBody] StoreDayLockRequest request)
        {
            // Chỉ Admin mới được mở khóa
            if (!_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                var rs = await _storeService.UnlockDay(request.StoreId, request.BusinessDate);
                return Ok(ResultAPI.Success(rs, $"Đã mở khóa ngày {request.BusinessDate:dd/MM/yyyy} thành công."));
            }
            catch (KeyNotFoundException ex) { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
        }
    }

    public class StoreDayLockRequest
    {
        public Guid     StoreId      { get; set; }
        public DateOnly BusinessDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public string?  Note         { get; set; }
    }

    public class SearchStoreRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 1000;
        /// <summary>StoreName | StoreCode | IsActive | CreatedAt</summary>
        public string? SortBy    { get; set; } = "StoreName";
        public bool    SortDesc  { get; set; } = false;
    }
}
