using CrediFlow.API.Services;
using CrediFlow.API.Models;
using CrediFlow.Common.Models;
using CrediFlow.Common.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrediFlow.API.Controllers
{
    [Authorize]
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly IReportService       _reportService;
        private readonly IUserInfoService      _userInfoService;
        private readonly IDataAccessLogService _dataAccessLog;

        public ReportController(IReportService reportService, IUserInfoService userInfoService,
                                IDataAccessLogService dataAccessLog)
        {
            _reportService   = reportService;
            _userInfoService = userInfoService;
            _dataAccessLog   = dataAccessLog;
        }

        private bool CanViewDashboardSnapshot()
        {
            return _userInfoService.IsAdmin
                || _userInfoService.IsRegionalManager
                || _userInfoService.IsStoreManager
                || _userInfoService.RoleCode == RoleCode.Staff;
        }

        // POST api/Report/DailyCollection
        /// <summary>Thu tiền hàng ngày theo cửa hàng và khoảng ngày.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> DailyCollection([FromBody] ReportDateRangeRequest request)
        {
            if (!CanViewDashboardSnapshot())
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetDailyCollection(request.StoreId, request.FromDate, request.ToDate);
            await _dataAccessLog.LogAsync("REPORT_DAILY_COLLECTION", null, "VIEW",
                queryParams: $"storeId={request.StoreId}&from={request.FromDate}&to={request.ToDate}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/OutstandingLoans
        /// <summary>Tổng tiền cho vay đang thu theo cửa hàng.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> OutstandingLoans([FromBody] ReportStoreRequest request)
        {
            if (!CanViewDashboardSnapshot())
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetOutstandingLoans(request.StoreId);
            await _dataAccessLog.LogAsync("REPORT_OUTSTANDING_LOANS", null, "VIEW",
                queryParams: $"storeId={request.StoreId}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/CustomerStats
        /// <summary>Thống kê khách CTV / Vãng lai / Cũ theo ngày, tuần, tháng.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> CustomerStats([FromBody] CustomerStatsRequest request)
        {
            // Chỉ StoreManager hoặc Admin xem báo cáo
            if (!_userInfoService.IsStoreManager && !_userInfoService.IsRegionalManager && !_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetCustomerStats(
                request.StoreId,
                request.FromDate,
                request.ToDate,
                request.GroupBy ?? "day");
            await _dataAccessLog.LogAsync("REPORT_CUSTOMER_STATS", null, "VIEW",
                queryParams: $"storeId={request.StoreId}&from={request.FromDate}&to={request.ToDate}&groupBy={request.GroupBy}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/OverdueSummary
        /// <summary>Báo cáo chậm nộp: tổng tiền theo mốc số ngày.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> OverdueSummary([FromBody] ReportStoreRequest request)
        {
            if (!CanViewDashboardSnapshot())
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetOverdueSummary(request.StoreId);
            await _dataAccessLog.LogAsync("REPORT_OVERDUE_SUMMARY", null, "VIEW",
                queryParams: $"storeId={request.StoreId}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/BadDebtSummary
        /// <summary>Báo cáo nợ xấu: phát sinh, thu hồi trong tháng, thu hồi tháng trước.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> BadDebtSummary([FromBody] BadDebtSummaryRequest request)
        {
            if (!CanViewDashboardSnapshot())
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetBadDebtSummary(request.StoreId, request.Year, request.Month);
            await _dataAccessLog.LogAsync("REPORT_BAD_DEBT_SUMMARY", null, "VIEW",
                queryParams: $"storeId={request.StoreId}&year={request.Year}&month={request.Month}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/IncomeBreakdown
        /// <summary>Tổng hợp thu nhập: lãi, phí, bảo hiểm, phạt tất toán sớm.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> IncomeBreakdown([FromBody] ReportDateRangeRequest request)
        {
            // Chỉ StoreManager hoặc Admin
            if (!_userInfoService.IsStoreManager && !_userInfoService.IsRegionalManager && !_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetIncomeBreakdown(request.StoreId, request.FromDate, request.ToDate);
            await _dataAccessLog.LogAsync("REPORT_INCOME_BREAKDOWN", null, "VIEW",
                queryParams: $"storeId={request.StoreId}&from={request.FromDate}&to={request.ToDate}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/MonthlyCashFlow
        /// <summary>Dòng tiền theo tháng trong năm: giải ngân / thu về / lợi nhuận.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> MonthlyCashFlow([FromBody] MonthlyCashFlowRequest request)
        {
            if (!CanViewDashboardSnapshot())
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetMonthlyCashFlow(request.StoreId, request.Year);
            await _dataAccessLog.LogAsync("REPORT_MONTHLY_CASH_FLOW", null, "VIEW",
                queryParams: $"storeId={request.StoreId}&year={request.Year}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/LoanPortfolioSummary
        /// <summary>Tổng quan danh mục: dư nợ, hợp đồng theo trạng thái, tỷ lệ thu hồi.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> LoanPortfolioSummary([FromBody] ReportStoreRequest request)
        {
            if (!CanViewDashboardSnapshot())
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetLoanPortfolioSummary(request.StoreId);
            await _dataAccessLog.LogAsync("REPORT_LOAN_PORTFOLIO", null, "VIEW",
                queryParams: $"storeId={request.StoreId}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/EmployeeKpi
        /// <summary>KPI nhân viên: hợp đồng, tổng giải ngân, thu hồi, tỷ lệ nợ xấu.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> EmployeeKpi([FromBody] ReportDateRangeRequest request)
        {
            // Chỉ StoreManager hoặc Admin
            if (!_userInfoService.IsStoreManager && !_userInfoService.IsRegionalManager && !_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetEmployeeKpi(request.StoreId, request.FromDate, request.ToDate);
            await _dataAccessLog.LogAsync("REPORT_EMPLOYEE_KPI", null, "VIEW",
                queryParams: $"storeId={request.StoreId}&from={request.FromDate}&to={request.ToDate}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/CustomerAdvancedStats
        /// <summary>Thống kê nâng cao về khách hàng: tỷ lệ tất toán / nợ xấu / đang nợ, khách quay lại, so sánh chi nhánh.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> CustomerAdvancedStats([FromBody] ReportDateRangeRequest request)
        {
            if (!_userInfoService.IsStoreManager && !_userInfoService.IsRegionalManager && !_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetCustomerAdvancedStats(request.StoreId, request.FromDate, request.ToDate);
            await _dataAccessLog.LogAsync("REPORT_CUSTOMER_ADVANCED", null, "VIEW",
                queryParams: $"storeId={request.StoreId}&from={request.FromDate}&to={request.ToDate}");
            return Ok(ResultAPI.Success(rs));
        }

        // POST api/Report/CollateralReport
        /// <summary>Báo cáo tài sản đảm bảo: tỷ lệ bễ, thu hồi trung bình tháng/năm.</summary>
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> CollateralReport([FromBody] ReportDateRangeRequest request)
        {
            if (!_userInfoService.IsStoreManager && !_userInfoService.IsRegionalManager && !_userInfoService.IsAdmin)
                return Ok(ResultAPI.ResultWithAccessDenined());

            var rs = await _reportService.GetCollateralReport(request.StoreId, request.FromDate, request.ToDate);
            await _dataAccessLog.LogAsync("REPORT_COLLATERAL", null, "VIEW",
                queryParams: $"storeId={request.StoreId}&from={request.FromDate}&to={request.ToDate}");
            return Ok(ResultAPI.Success(rs));
        }
    }

    public class MonthlyCashFlowRequest
    {
        public Guid? StoreId { get; set; }
        public int   Year    { get; set; } = DateTime.Today.Year;
    }

    public class ReportDateRangeRequest
    {
        public Guid?    StoreId  { get; set; }
        public DateOnly FromDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly ToDate   { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }

    public class ReportStoreRequest
    {
        public Guid? StoreId { get; set; }
    }

    public class CustomerStatsRequest
    {
        public Guid?    StoreId  { get; set; }
        public DateOnly FromDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
        public DateOnly ToDate   { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        /// <summary>Nhóm theo: "day" | "week" | "month"</summary>
        public string?  GroupBy  { get; set; } = "day";
    }

    public class BadDebtSummaryRequest
    {
        public Guid? StoreId { get; set; }
        public int   Year    { get; set; } = DateTime.Today.Year;
        public int   Month   { get; set; } = DateTime.Today.Month;
    }
}
