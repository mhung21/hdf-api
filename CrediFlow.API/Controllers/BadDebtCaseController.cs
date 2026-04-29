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
    public class BadDebtCaseController : ControllerBase
    {
        private readonly IBadDebtCaseService _badDebtCaseService;
        private readonly IUserInfoService    _userInfoService;

        public BadDebtCaseController(IBadDebtCaseService badDebtCaseService, IUserInfoService userInfoService)
        {
            _badDebtCaseService = badDebtCaseService;
            _userInfoService    = userInfoService;
        }

        // GET api/BadDebtCase/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _badDebtCaseService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/BadDebtCase/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _badDebtCaseService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy hồ sơ nợ xấu.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/BadDebtCase/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchBadDebtCaseRequest request)
        {
            var rs = await _badDebtCaseService.SearchBadDebtCase(
                request.Keyword   ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/BadDebtCase/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CUBadDebtCaseModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            if ((_userInfoService.IsStoreManager || _userInfoService.IsRegionalManager) && !_userInfoService.IsAdmin)
            {
                if (!_userInfoService.GetStoreScopeIds(model.StoreId).Any())
                    return Ok(ResultAPI.Error(null, "Bạn không có quyền thao tác hồ sơ nợ xấu ngoài chi nhánh của mình."));
            }

            bool isUpdate = model.BadDebtCaseId.HasValue && model.BadDebtCaseId != Guid.Empty;
            try
            {
                var rs = await _badDebtCaseService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} hồ sơ nợ xấu thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }

        // POST api/BadDebtCase/TransferFromLoan
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> TransferFromLoan([FromBody] TransferBadDebtRequest request)
        {
            // Chỉ StoreManager hoặc Admin mới được chuyển nợ xấu
            if (!_userInfoService.IsStoreManager && !_userInfoService.IsRegionalManager && !_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                var rs = await _badDebtCaseService.TransferFromLoan(request.LoanContractId, request.Note);
                return Ok(ResultAPI.Success(rs, "Đã chuyển hợp đồng sang nợ xấu thành công."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex) { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
        }

        // POST api/BadDebtCase/RecordRecovery
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> RecordRecovery([FromBody] RecordBadDebtRecoveryModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            // Cần tối thiểu quyền StoreManager để ghi nhận thu hồi
            if (!_userInfoService.IsStoreManager && !_userInfoService.IsRegionalManager && !_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                var rs = await _badDebtCaseService.RecordRecovery(model);
                return Ok(ResultAPI.Success(rs, "Đã ghi nhận khoản thu hồi nợ xấu thành công."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex) { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
        }
    }

    public class TransferBadDebtRequest
    {
        public Guid    LoanContractId { get; set; }
        public string? Note           { get; set; }
    }

    public class SearchBadDebtCaseRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 10;
        /// <summary>TransferredAt | StatusCode | TotalOutstandingAmount | CreatedAt</summary>
        public string? SortBy    { get; set; } = "TransferredAt";
        public bool    SortDesc  { get; set; } = true;
    }
}
