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
    public class LoanChargeController : ControllerBase
    {
        private readonly ILoanChargeService _loanChargeService;
        private readonly IUserInfoService   _userInfoService;

        public LoanChargeController(ILoanChargeService loanChargeService, IUserInfoService userInfoService)
        {
            _loanChargeService = loanChargeService;
            _userInfoService   = userInfoService;
        }

        // GET api/LoanCharge/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _loanChargeService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanCharge/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _loanChargeService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy khoản phí.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanCharge/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchLoanChargeRequest request)
        {
            var rs = await _loanChargeService.SearchLoanCharge(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanCharge/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CULoanChargeModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            bool isUpdate = model.ChargeId.HasValue && model.ChargeId != Guid.Empty;
            try
            {
                var rs = await _loanChargeService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} khoản phí {model.ChargeName} thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }
    }

    public class SearchLoanChargeRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 10;
        /// <summary>DueDate | ChargeDate | StatusCode | Amount | CreatedAt</summary>
        public string? SortBy    { get; set; } = "DueDate";
        public bool    SortDesc  { get; set; } = false;
    }
}
