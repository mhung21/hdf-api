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
    public class LoanSettlementController : ControllerBase
    {
        private readonly ILoanSettlementService _loanSettlementService;
        private readonly IUserInfoService       _userInfoService;

        public LoanSettlementController(ILoanSettlementService loanSettlementService, IUserInfoService userInfoService)
        {
            _loanSettlementService = loanSettlementService;
            _userInfoService       = userInfoService;
        }

        // GET api/LoanSettlement/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _loanSettlementService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanSettlement/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _loanSettlementService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy phiếu tất toán.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanSettlement/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchLoanSettlementRequest request)
        {
            var rs = await _loanSettlementService.SearchLoanSettlement(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanSettlement/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CULoanSettlementModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            bool isUpdate = model.SettlementId.HasValue && model.SettlementId != Guid.Empty;
            try
            {
                var rs = await _loanSettlementService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} phiếu tất toán {rs.SettlementNo} thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }

        // POST api/LoanSettlement/Calculate
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Calculate([FromBody] CalculateSettlementRequest request)
        {
            try
            {
                var rs = await _loanSettlementService.Calculate(request.LoanContractId, request.SettlementDate);
                return Ok(ResultAPI.Success(rs));
            }
            catch (KeyNotFoundException ex) { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
        }
    }

    public class CalculateSettlementRequest
    {
        public Guid    LoanContractId { get; set; }
        public DateOnly SettlementDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }

    public class SearchLoanSettlementRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 10;
        /// <summary>RequestDate | SettlementNo | SettlementType | TotalSettlementAmount | CreatedAt</summary>
        public string? SortBy    { get; set; } = "RequestDate";
        public bool    SortDesc  { get; set; } = true;
    }
}
