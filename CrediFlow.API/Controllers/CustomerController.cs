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
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly IUserInfoService _userInfoService;

        public CustomerController(ICustomerService customerService, IUserInfoService userInfoService)
        {
            _customerService = customerService;
            _userInfoService = userInfoService;
        }

        // GET api/Customer/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _customerService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Customer/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _customerService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy khách hàng.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Customer/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchCustomerRequest request)
        {
            var filterStoreIds = (_userInfoService.IsAdmin && request.FilterStoreIds != null && request.FilterStoreIds.Any())
                ? request.FilterStoreIds
                : null;

            var rs = await _customerService.SearchCustomer(
                request.Keyword  ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc,
                request.HasBadDebt,
                request.HasActiveLoan,
                filterStoreIds);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Customer/GetByNationalId
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetByNationalId([FromBody] string nationalId)
        {
            if (string.IsNullOrWhiteSpace(nationalId))
                return Ok(ResultAPI.Error(null, "Số CCCD không được để trống.", 400));

            var rs = await _customerService.GetByNationalId(nationalId);
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Customer/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUCustomerModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            bool isUpdate = model.CustomerId.HasValue && model.CustomerId != Guid.Empty;

            // StoreManager chỉ được tạo / sửa KH thuộc chi nhánh của mình
            if ((_userInfoService.IsStoreManager || _userInfoService.IsRegionalManager) && !_userInfoService.IsAdmin)
            {
                if (!model.FirstStoreId.HasValue || !_userInfoService.GetStoreScopeIds(model.FirstStoreId).Any())
                    return Ok(ResultAPI.Error(null, "Bạn không có quyền thao tác với khách hàng ngoài chi nhánh của mình."));
            }

            try
            {
                var rs = await _customerService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} khách hàng {model.FullName} thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (ArgumentException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 400));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 403));
            }
        }

        // POST api/Customer/Assign
        /// <summary>Chuyển giao khách hàng cho nhân viên khác phụ trách (chỉ Manager/Admin).</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Assign([FromBody] AssignCustomerRequest request)
        {
            try
            {
                await _customerService.AssignCustomer(request.CustomerId, request.TargetUserId);
                return Ok(ResultAPI.Success(null, "Chuyển giao khách hàng thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 403));
            }
        }
    }

    /// <summary>Request body cho API tìm kiếm khách hàng.</summary>
    public class SearchCustomerRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 1000;
        /// <summary>Trường sắp xếp: FullName | NationalId | CustomerCode | Phone | CreatedAt | UpdatedAt</summary>
        public string? SortBy    { get; set; } = "FullName";
        /// <summary>true = giảm dần, false = tăng dần.</summary>
        public bool    SortDesc  { get; set; } = false;
        /// <summary>Lọc theo nợ xấu: true = chỉ khách có nợ xấu, false = không có nợ xấu, null = tất cả.</summary>
        public bool?   HasBadDebt    { get; set; }
        /// <summary>Lọc theo hợp đồng đang hoạt động: true = đang vay, false = không đang vay, null = tất cả.</summary>
        public bool?   HasActiveLoan { get; set; }
        /// <summary>Lọc theo một hoặc nhiều chi nhánh. Hỗ trợ multi-select trên FE.</summary>
        public List<Guid>? FilterStoreIds { get; set; }
    }

    public class AssignCustomerRequest
    {
        public Guid CustomerId   { get; set; }
        /// <summary>Id của nhân viên sẽ tiếp nhận quản lý khách hàng.</summary>
        public Guid TargetUserId { get; set; }
    }
}
