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
    public class LoanProductController : ControllerBase
    {
        private readonly ILoanProductService _loanProductService;
        private readonly IUserInfoService    _userInfoService;

        public LoanProductController(ILoanProductService loanProductService, IUserInfoService userInfoService)
        {
            _loanProductService = loanProductService;
            _userInfoService    = userInfoService;
        }

        // GET api/LoanProduct/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _loanProductService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanProduct/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _loanProductService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy sản phẩm vay.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanProduct/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchLoanProductRequest request)
        {
            var rs = await _loanProductService.SearchLoanProduct(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanProduct/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CULoanProductModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            // StoreManager chỉ được tạo sản phẩm cho chi nhánh của mình
            if ((_userInfoService.IsStoreManager || _userInfoService.IsRegionalManager) && !_userInfoService.IsAdmin)
            {
                if (model.StoreId.HasValue && !_userInfoService.GetStoreScopeIds(model.StoreId).Any())
                    return Ok(ResultAPI.Error(null, "Bạn không có quyền tạo sản phẩm cho chi nhánh khác."));
            }

            bool isUpdate = model.LoanProductId.HasValue && model.LoanProductId != Guid.Empty;
            try
            {
                var rs = await _loanProductService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} sản phẩm {model.ProductName} thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }
    }

    public class SearchLoanProductRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 10;
        /// <summary>ProductName | ProductCode | IsActive | CreatedAt</summary>
        public string? SortBy    { get; set; } = "ProductName";
        public bool    SortDesc  { get; set; } = false;
    }
}
