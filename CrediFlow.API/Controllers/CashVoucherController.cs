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
    public class CashVoucherController : ControllerBase
    {
        private readonly ICashVoucherService _cashVoucherService;
        private readonly IUserInfoService    _userInfoService;

        public CashVoucherController(ICashVoucherService cashVoucherService, IUserInfoService userInfoService)
        {
            _cashVoucherService = cashVoucherService;
            _userInfoService    = userInfoService;
        }

        // GET api/CashVoucher/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _cashVoucherService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/CashVoucher/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _cashVoucherService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy phiếu thu/chi.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/CashVoucher/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchCashVoucherRequest request)
        {
            var rs = await _cashVoucherService.SearchCashVoucher(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc,
                request.LoanContractId);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/CashVoucher/GetByLoanContract
        /// <summary>Lấy danh sách phiếu thu thuộc một hợp đồng (kèm allocation).</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetByLoanContract([FromBody] Guid loanContractId)
        {
            var rs = await _cashVoucherService.GetByLoanContract(loanContractId);
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/CashVoucher/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUCashVoucherModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            if ((_userInfoService.IsStoreManager || _userInfoService.IsRegionalManager) && !_userInfoService.IsAdmin)
            {
                var effectiveStoreId = model.StoreId ?? _userInfoService.StoreId;
                if (!effectiveStoreId.HasValue || !_userInfoService.GetStoreScopeIds(effectiveStoreId).Any())
                    return Ok(ResultAPI.Error(null, "Bạn không có quyền tạo phiếu cho chi nhánh khác."));
            }

            bool isUpdate = model.VoucherId.HasValue && model.VoucherId != Guid.Empty;
            try
            {
                var rs = await _cashVoucherService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} phiếu {rs.VoucherNo} thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }

        // POST api/CashVoucher/CollectLoanPayment
        /// <summary>
        /// Thu tiền khoản vay: tạo phiếu thu + phân bổ từng khoản + cập nhật lịch/phí tự động.
        /// Dùng thay cho Save() khi thu tiền liên quan đến hợp đồng.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> CollectLoanPayment([FromBody] CollectLoanPaymentModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            try
            {
                var rs = await _cashVoucherService.CollectLoanPayment(model);
                return Ok(ResultAPI.Success(rs, $"Tạo phiếu thu {rs.VoucherNo} thành công."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex) { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
        }
    }

    public class SearchCashVoucherRequest
    {
        public string? Keyword        { get; set; }
        public int     PageIndex      { get; set; } = 1;
        public int     PageSize       { get; set; } = 10;
        /// <summary>VoucherNo | BusinessDate | VoucherType | Amount | CreatedAt | VoucherDatetime</summary>
        public string? SortBy         { get; set; } = "VoucherDatetime";
        public bool    SortDesc       { get; set; } = true;
        /// <summary>Lọc phiếu theo hợp đồng cụ thể.</summary>
        public Guid?   LoanContractId { get; set; }
    }
}
