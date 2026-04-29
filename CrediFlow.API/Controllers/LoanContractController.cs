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
    public class LoanContractController : ControllerBase
    {
        private readonly ILoanContractService _loanContractService;
        private readonly IUserInfoService     _userInfoService;

        public LoanContractController(ILoanContractService loanContractService, IUserInfoService userInfoService)
        {
            _loanContractService = loanContractService;
            _userInfoService     = userInfoService;
        }

        // GET api/LoanContract/GetAll
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> GetAll()
        {
            var rs = await _loanContractService.GetAlls();
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanContract/GetById
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetById([FromBody] Guid id)
        {
            var rs = await _loanContractService.GetAsync(id);
            if (rs == null)
                return Ok(ResultAPI.Error(null, "Không tìm thấy hợp đồng.", 404));

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanContract/Search
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Search([FromBody] SearchLoanContractRequest request)
        {
            // Chỉ Admin mới được lọc theo danh sách cửa hàng tuỳ chọn
            List<Guid>? filterStoreIds = (_userInfoService.IsAdmin && request.FilterStoreIds != null && request.FilterStoreIds.Any())
                ? request.FilterStoreIds
                : null;

            var rs = await _loanContractService.SearchLoanContract(
                request.Keyword      ?? string.Empty,
                request.PageIndex,
                request.PageSize,
                request.SortBy,
                request.SortDesc,
                request.StatusCode,
                request.DateFilterType,
                request.FromDate,
                request.ToDate,
                filterStoreIds);

            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanContract/Save
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Save([FromBody] CULoanContractModel model)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            // StoreManager chỉ được tạo hợp đồng thuộc chi nhánh mình
            if ((_userInfoService.IsStoreManager || _userInfoService.IsRegionalManager) && !_userInfoService.IsAdmin)
            {
                if (!_userInfoService.GetStoreScopeIds(model.StoreId).Any())
                    return Ok(ResultAPI.Error(null, "Bạn không có quyền tạo hợp đồng cho chi nhánh khác."));
            }

            bool isUpdate = model.LoanContractId.HasValue && model.LoanContractId != Guid.Empty;
            try
            {
                var rs = await _loanContractService.Save(model);
                return Ok(ResultAPI.Success(rs, $"{(isUpdate ? "Cập nhật" : "Thêm mới")} hợp đồng thành công."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex) { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
        }

        // POST api/LoanContract/Calculate
        [HttpPost]
        public ActionResult<ResultAPI> Calculate([FromBody] LoanCalculateRequest request)
        {
            if (request.PrincipalAmount <= 0 || request.TermMonths <= 0)
                return Ok(ResultAPI.Error(null, "Số tiền vay và số kỳ phải lớn hơn 0.", 400));

            var rs = _loanContractService.Calculate(request);
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanContract/Cancel
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Cancel([FromBody] CancelLoanContractRequest request)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            // Chỉ StoreManager hoặc Admin mới được hủy hợp đồng
            if (!_userInfoService.IsStoreManager && !_userInfoService.IsRegionalManager && !_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            try
            {
                var rs = await _loanContractService.Cancel(request.LoanContractId, request.CancellationReason);
                return Ok(ResultAPI.Success(rs, $"Đã hủy hợp đồng {rs.ContractNo} thành công."));
            }
            catch (KeyNotFoundException ex)      { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex) { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
        }

        // POST api/LoanContract/GetRepaymentSchedule
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetRepaymentSchedule([FromBody] Guid loanContractId)
        {
            var rs = await _loanContractService.GetRepaymentSchedule(loanContractId);
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanContract/GetOverdueList
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetOverdueList([FromBody] GetOverdueListRequest request)
        {
            var rs = await _loanContractService.GetOverdueList(request.MinDaysOverdue, request.MaxDaysOverdue);
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanContract/ChangeStatus
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> ChangeStatus([FromBody] ChangeStatusRequest request)
        {
            if (!ModelState.IsValid)
                return Ok(ResultAPI.Error(ModelState, "Dữ liệu không hợp lệ.", 400));

            try
            {
                var rs = await _loanContractService.ChangeStatus(
                    request.LoanContractId, request.ToStatus, request.Reason);
                var label = request.ToStatus switch
                {
                    "PENDING_APPROVAL"      => "Chờ duyệt",
                    "PENDING_DISBURSEMENT"  => "Chờ giải ngân",
                    "DISBURSED"             => "Đang thu",
                    "BAD_DEBT"              => "Nợ xấu",
                    "SETTLED"               => "Đã tất toán",
                    "CLOSED"                => "Đã đóng",
                    "BAD_DEBT_CLOSED"       => "Đã đóng (nợ xấu)",
                    "CANCELLED"             => "Đã hủy",
                    "DRAFT"                 => "Nháp",
                    _                       => request.ToStatus,
                };
                return Ok(ResultAPI.Success(rs, $"Đã chuyển hợp đồng sang trạng thái '{label}' thành công."));
            }
            catch (KeyNotFoundException ex)        { return Ok(ResultAPI.Error(null, ex.Message, 404)); }
            catch (InvalidOperationException ex)   { return Ok(ResultAPI.Error(null, ex.Message, 400)); }
            catch (UnauthorizedAccessException ex) { return Ok(ResultAPI.Error(null, ex.Message, 403)); }
        }

        // POST api/LoanContract/GetStatusHistory
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetStatusHistory([FromBody] Guid loanContractId)
        {
            var rs = await _loanContractService.GetStatusHistory(loanContractId);
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/LoanContract/GetFinancialSummary
        /// <summary>Tóm tắt tài chính hợp đồng: tổng giải ngân, đã thu, còn lại, lãi/phí/phạt.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> GetFinancialSummary([FromBody] Guid loanContractId)
        {
            try
            {
                var rs = await _loanContractService.GetFinancialSummary(loanContractId);
                return Ok(ResultAPI.Success(rs));
            }
            catch (KeyNotFoundException ex)
            {
                return Ok(ResultAPI.Error(null, ex.Message, 404));
            }
        }

        // POST api/LoanContract/Assign
        /// <summary>Chuyển giao hợp đồng cho nhân viên khác phụ trách (chỉ Manager/Admin).</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> Assign([FromBody] AssignLoanContractRequest request)
        {
            try
            {
                var rs = await _loanContractService.AssignLoanContract(request.LoanContractId, request.TargetUserId);
                return Ok(ResultAPI.Success(rs, "Chuyển giao hợp đồng thành công."));
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

    public class CancelLoanContractRequest
    {
        public Guid   LoanContractId     { get; set; }
        public string CancellationReason { get; set; } = string.Empty;
    }

    public class GetOverdueListRequest
    {
        public int? MinDaysOverdue { get; set; }
        public int? MaxDaysOverdue { get; set; }
    }

    public class SearchLoanContractRequest
    {
        public string? Keyword   { get; set; }
        public int     PageIndex { get; set; } = 1;
        public int     PageSize  { get; set; } = 10;
        /// <summary>ContractNo | ApplicationDate | StatusCode | PrincipalAmount | CreatedAt</summary>
        public string? SortBy          { get; set; } = "CreatedAt";
        public bool    SortDesc        { get; set; } = true;
        // ── Bộ lọc bổ sung ──
        /// <summary>Lọc theo trạng thái hợp đồng (VD: DRAFT, DISBURSED...)</summary>
        public string?   StatusCode     { get; set; }
        /// <summary>Ngày bắt đầu khoảng lọc</summary>
        public DateOnly? FromDate       { get; set; }
        /// <summary>Ngày kết thúc khoảng lọc</summary>
        public DateOnly? ToDate         { get; set; }
        /// <summary>Loại ngày lọc: ApplicationDate | DisbursedDate</summary>
        public string?   DateFilterType { get; set; }
        /// <summary>Lọc theo danh sách cửa hàng — chỉ Admin mới dùng được</summary>
        public List<Guid>? FilterStoreIds { get; set; }
    }

    public class ChangeStatusRequest
    {
        public Guid   LoanContractId { get; set; }
        /// <summary>Trạng thái đích. Xem LoanContractStatus để biết các giá trị hợp lệ.</summary>
        public string ToStatus       { get; set; } = string.Empty;
        /// <summary>Lý do chuyển trạng thái (bắt buộc khi hủy hoặc từ chối).</summary>
        public string? Reason        { get; set; }
    }

    public class AssignLoanContractRequest
    {
        public Guid LoanContractId { get; set; }
        /// <summary>Id của nhân viên sẽ tiếp nhận quản lý hợp đồng.</summary>
        public Guid TargetUserId   { get; set; }
    }
}

