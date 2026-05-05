using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface IReportService
    {
        /// <summary>Báo cáo thu tiền hàng ngày (từ view v_daily_collection).</summary>
        Task<object> GetDailyCollection(Guid? storeId, DateOnly fromDate, DateOnly toDate);

        /// <summary>Danh sách hợp đồng đang thu (status DISBURSED) theo cửa hàng.</summary>
        Task<object> GetOutstandingLoans(Guid? storeId);

        /// <summary>Thống kê khách hàng mới/cũ/CTV theo ngày, tuần, hoặc tháng.</summary>
        Task<object> GetCustomerStats(Guid? storeId, DateOnly fromDate, DateOnly toDate, string groupBy);

        /// <summary>Báo cáo chậm nộp: tổng số tiền chậm theo từng mốc ngày.</summary>
        Task<object> GetOverdueSummary(Guid? storeId);

        /// <summary>Báo cáo nợ xấu: phát sinh tháng này, thu hồi tháng này, thu hồi tháng trước.</summary>
        Task<object> GetBadDebtSummary(Guid? storeId, int year, int month);

        /// <summary>Tổng hợp thu nhập: lãi, phí, bảo hiểm, phạt tất toán sớm theo kỳ.</summary>
        Task<object> GetIncomeBreakdown(Guid? storeId, DateOnly fromDate, DateOnly toDate);

        /// <summary>Dòng tiền theo tháng: giải ngân / thu về / lợi nhuận (lãi+phí).</summary>
        Task<object> GetMonthlyCashFlow(Guid? storeId, int year);

        /// <summary>Tổng quan danh mục: dư nợ, hợp đồng theo trạng thái, tý lệ thu hồi.</summary>
        Task<object> GetLoanPortfolioSummary(Guid? storeId);

        /// <summary>KPI nhân viên: hợp đồng tạo/giải ngân/tất toán/nợ xấu, tổng gốc, số KH tạo mới, phiếu thu.</summary>
        Task<object> GetEmployeeKpi(Guid? storeId, DateOnly fromDate, DateOnly toDate);

        /// <summary>Thống kê nâng cao về khách hàng: tỷ lệ tất toán/nợ xấu/đang nợ, khách quay lại, theo chi nhánh.</summary>
        Task<object> GetCustomerAdvancedStats(Guid? storeId, DateOnly fromDate, DateOnly toDate);

        /// <summary>Báo cáo tài sản đảm bảo: theo loại, tỷ lệ bễ, thu hồi trung bình tháng/năm.</summary>
        Task<object> GetCollateralReport(Guid? storeId, DateOnly fromDate, DateOnly toDate);
    }

    public class ReportService : IReportService
    {
        private readonly CrediflowContext _dbContext;
        private readonly IUserInfoService _user;

        public ReportService(CrediflowContext dbContext, IUserInfoService user)
        {
            _dbContext = dbContext;
            _user      = user;
            
        }

        // ──────────────────────────────────────────────────────────────────────
        // Thu tiền hàng ngày
        // ──────────────────────────────────────────────────────────────────────

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId) => _user.GetStoreScopeIds(storeId);

        public async Task<object> GetDailyCollection(Guid? storeId, DateOnly fromDate, DateOnly toDate)
        {
            var query = _dbContext.VDailyCollections.AsQueryable();

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                query = query.Where(v => storeScopeIds.Any(id => id == v.StoreId));

            query = query.Where(v => v.BusinessDate >= fromDate && v.BusinessDate <= toDate);

            var rows = await query.OrderBy(v => v.BusinessDate).ThenBy(v => v.StoreName).ToListAsync();

            // Ánh xạ field name cho frontend
            return rows.Select(v => new
            {
                BusinessDate  = v.BusinessDate,
                StoreName     = v.StoreName,
                TotalReceipt  = v.TotalReceipts,
                TotalPayment  = v.TotalPayments,
                NetAmount     = v.NetCashflow,
                ReceiptCount  = v.VoucherCount,
            }).ToList();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Tổng dư nợ đang thu theo cửa hàng
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> GetOutstandingLoans(Guid? storeId)
        {
            var query = _dbContext.LoanContracts
                .Where(c => c.StatusCode == "DISBURSED")
                .AsQueryable();

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                query = query.Where(c => storeScopeIds.Any(id => id == c.StoreId));

            var rows = await query
                .Select(c => new
                {
                    LoanContractId    = c.LoanContractId,
                    ContractCode      = c.ContractNo,
                    CustomerName      = c.Customer.FullName,
                    StoreId           = c.StoreId,
                    StoreName         = c.Store.StoreName,
                    // Gốc hiệu dụng = gốc thực + bảo hiểm (đã gộp vào lịch trả nợ)
                    PrincipalAmount   = c.PrincipalAmount + c.InsuranceAmountSnapshot,
                    RemainingPrincipal = (c.PrincipalAmount + c.InsuranceAmountSnapshot)
                                        - (c.LoanRepaymentSchedules.Sum(s => (decimal?)s.PaidPrincipalAmount) ?? 0),
                    DisbursedDate     = c.DisbursementDate,
                    MaturityDate      = c.MaturityDate,
                    StatusCode        = c.StatusCode,
                })
                .OrderBy(r => r.StoreName)
                .ThenBy(r => r.MaturityDate)
                .ToListAsync();

            return rows;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Thống kê khách hàng (CTV / Vãng lai / Cũ) theo ngày/tuần/tháng
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> GetCustomerStats(Guid? storeId, DateOnly fromDate, DateOnly toDate, string groupBy)
        {
            var query = _dbContext.CustomerVisits
                .Where(v => v.VisitDate >= fromDate && v.VisitDate <= toDate)
                .AsQueryable();

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                query = query.Where(v => storeScopeIds.Any(id => id == v.StoreId));

            var raw = await query
                .Select(v => new
                {
                    v.VisitDate,
                    v.VisitType,
                    SourceType = v.Customer != null ? v.Customer.FirstSourceType : null
                })
                .ToListAsync();

            // Nhóm theo ngày / tuần / tháng
            var grouped = (groupBy?.ToLower() ?? "day") switch
            {
                "week"  => raw.GroupBy(v => $"{System.Globalization.ISOWeek.GetYear(v.VisitDate.ToDateTime(TimeOnly.MinValue))}-W{System.Globalization.ISOWeek.GetWeekOfYear(v.VisitDate.ToDateTime(TimeOnly.MinValue)):D2}").Select(g => new
                {
                    Period    = g.Key,
                    NewCtv    = g.Count(v => v.VisitType == VisitType.New && v.SourceType == SourceType.Ctv),
                    NewVangLai= g.Count(v => v.VisitType == VisitType.New && v.SourceType == SourceType.VangLai),
                    OldCustomer = g.Count(v => v.VisitType == VisitType.Old),
                    Total     = g.Count()
                }),
                "month" => raw.GroupBy(v => $"{v.VisitDate.Year}-{v.VisitDate.Month:D2}").Select(g => new
                {
                    Period    = g.Key,
                    NewCtv    = g.Count(v => v.VisitType == VisitType.New && v.SourceType == SourceType.Ctv),
                    NewVangLai= g.Count(v => v.VisitType == VisitType.New && v.SourceType == SourceType.VangLai),
                    OldCustomer = g.Count(v => v.VisitType == VisitType.Old),
                    Total     = g.Count()
                }),
                _       => raw.GroupBy(v => v.VisitDate.ToString("yyyy-MM-dd")).Select(g => new
                {
                    Period    = g.Key,
                    NewCtv    = g.Count(v => v.VisitType == VisitType.New && v.SourceType == SourceType.Ctv),
                    NewVangLai= g.Count(v => v.VisitType == VisitType.New && v.SourceType == SourceType.VangLai),
                    OldCustomer = g.Count(v => v.VisitType == VisitType.Old),
                    Total     = g.Count()
                })
            };

            var items = grouped.OrderBy(g => g.Period).ToList();
            return new
            {
                FromDate      = fromDate,
                ToDate        = toDate,
                GroupBy       = groupBy,
                TotalNew      = items.Sum(i => i.NewCtv + i.NewVangLai),
                TotalReturning= items.Sum(i => i.OldCustomer),
                TotalCtv      = items.Sum(i => i.NewCtv),
                TotalWalkIn   = items.Sum(i => i.NewVangLai),
                Rows          = items.Select(i => new
                {
                    Period             = i.Period,
                    NewCustomers       = i.NewCtv + i.NewVangLai,
                    ReturningCustomers = i.OldCustomer,
                    CtvCustomers       = i.NewCtv,
                    WalkInCustomers    = i.NewVangLai,
                    Total              = i.Total,
                }).ToList(),
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Báo cáo chậm nộp theo mốc ngày
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> GetOverdueSummary(Guid? storeId)
        {
            var query = _dbContext.VOverdueSchedules.AsQueryable();

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                query = query.Where(v => storeScopeIds.Any(id => id == v.StoreId));

            var rows = await query.ToListAsync();

            // Phân nhóm theo mốc: 4-10 / 11-20 / 21-30 / >30
            return new
            {
                TotalOverdueContracts = rows.Select(r => r.LoanContractId).Distinct().Count(),
                TotalUnpaid           = rows.Sum(r => r.TotalUnpaid ?? 0),
                Buckets = new[]
                {
                    new { Label = "4-10 ngày",  Count = rows.Count(r => r.DaysOverdue >=  4 && r.DaysOverdue <= 10), Amount = rows.Where(r => r.DaysOverdue >=  4 && r.DaysOverdue <= 10).Sum(r => r.TotalUnpaid ?? 0) },
                    new { Label = "11-20 ngày", Count = rows.Count(r => r.DaysOverdue >= 11 && r.DaysOverdue <= 20), Amount = rows.Where(r => r.DaysOverdue >= 11 && r.DaysOverdue <= 20).Sum(r => r.TotalUnpaid ?? 0) },
                    new { Label = "21-30 ngày", Count = rows.Count(r => r.DaysOverdue >= 21 && r.DaysOverdue <= 30), Amount = rows.Where(r => r.DaysOverdue >= 21 && r.DaysOverdue <= 30).Sum(r => r.TotalUnpaid ?? 0) },
                    new { Label = ">30 ngày",   Count = rows.Count(r => r.DaysOverdue >  30),                         Amount = rows.Where(r => r.DaysOverdue >  30).Sum(r => r.TotalUnpaid ?? 0) },
                },
                // Ánh xạ field name cho frontend (contractCode, overdueDays, overdueAmount, latePenaltyAmount)
                RiskDetail = rows
                    .OrderByDescending(r => r.DaysOverdue)
                    .Select(r => new
                    {
                        ScheduleId           = r.ScheduleId,
                        LoanContractId      = r.LoanContractId,
                        ContractCode        = r.ContractNo,
                        ContractNo          = r.ContractNo,
                        CustomerName        = r.CustomerName,
                        CustomerCode        = r.CustomerCode,
                        CustomerNationalId  = r.CustomerCode,
                        StoreName           = r.StoreName,
                        OverdueDays         = r.DaysOverdue,
                        DaysOverdue         = r.DaysOverdue,
                        OverdueAmount       = r.TotalUnpaid,
                        TotalUnpaid         = r.TotalUnpaid,
                        LatePenaltyAmount   = r.UnpaidPenalty,
                        DueDate             = r.DueDate,
                        RiskLevel           = string.IsNullOrWhiteSpace(r.RiskLevel)
                            ? ((r.DaysOverdue ?? 0) > 30 ? "POTENTIAL_BAD_DEBT"
                                : (r.DaysOverdue ?? 0) > 10 ? "LATE"
                                : "WARNING")
                            : r.RiskLevel,
                    })
                    .ToList(),
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Báo cáo nợ xấu
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> GetBadDebtSummary(Guid? storeId, int year, int month)
        {
            var query = _dbContext.BadDebtCases.AsQueryable();

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                query = query.Where(b => storeScopeIds.Any(id => id == b.StoreId));

            var allCases = await query.ToListAsync();

            // Lấy tất cả phiếu thu hồi nợ xấu trong năm
            var recoveryQuery = _dbContext.CashVouchers
                .Where(v => v.BusinessDate.Year == year
                         && v.ReasonCode == VoucherReasonCode.BadDebtRecovery);

            if (storeScopeIds is not null)
                recoveryQuery = recoveryQuery.Where(v => storeScopeIds.Any(id => id == v.StoreId));

            var recoveries = await recoveryQuery
                .Select(v => new { v.BusinessDate.Month, v.Amount })
                .ToListAsync();

            // Phát sinh từng tháng trong năm (tháng có dữ liệu hoặc tháng hiện tại)
            var months = allCases
                .Where(b => b.TransferredAt.Year == year)
                .GroupBy(b => b.TransferredAt.Month)
                .Select(g => g.Key)
                .Union(new[] { month })   // luôn hiển thị tháng hiện tại
                .OrderBy(m => m)
                .ToList();

            // Nợ xấu còn tồn (tính đến cuối từng tháng)
            var openCases = allCases.Where(b => b.StatusCode != BadDebtCaseStatus.Closed).ToList();
            var totalOutstanding = openCases.Sum(b => b.TotalOutstandingAmount - b.RecoveredAmountTotal);

            var result = months.Select(m => new
            {
                Month               = $"{year}-{m:D2}",
                StoreName           = (string?)null,
                NewCases            = allCases.Count(b => b.TransferredAt.Year == year && b.TransferredAt.Month == m),
                NewAmount           = allCases.Where(b => b.TransferredAt.Year == year && b.TransferredAt.Month == m)
                                             .Sum(b => b.TotalOutstandingAmount),
                RecoveredThisMonth  = recoveries.Where(r => r.Month == m).Sum(r => r.Amount),
                RecoveredPrevMonths = recoveries.Where(r => r.Month  < m).Sum(r => r.Amount),
                TotalOutstanding    = totalOutstanding,
            }).ToList();

            return result;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Tổng hợp thu nhập: lãi / phí / bảo hiểm / phạt tất toán sớm
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> GetIncomeBreakdown(Guid? storeId, DateOnly fromDate, DateOnly toDate)
        {
            var query = _dbContext.CashVoucherAllocations
                .Where(a => a.Voucher.BusinessDate >= fromDate && a.Voucher.BusinessDate <= toDate)
                .AsQueryable();

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                query = query.Where(a => storeScopeIds.Any(id => id == a.Voucher.StoreId));

            var allocations = await query
                .Select(a => new { a.ComponentCode, a.Amount })
                .ToListAsync();

            decimal Sum(string code) => allocations
                .Where(a => a.ComponentCode == code)
                .Sum(a => a.Amount);

            int Count(string code) => allocations.Count(a => a.ComponentCode == code);

            // Trả về dạng mảng cho frontend
            var categories = new[]
            {
                new { Category = VoucherComponentCode.Interest,               CategoryName = "Lãi",                   Amount = Sum(VoucherComponentCode.Interest),               Count = Count(VoucherComponentCode.Interest) },
                new { Category = VoucherComponentCode.PeriodicFee,            CategoryName = "Phí QLKV/QLTS",          Amount = Sum(VoucherComponentCode.PeriodicFee),            Count = Count(VoucherComponentCode.PeriodicFee) },
                new { Category = VoucherComponentCode.FileFee,                CategoryName = "Phí hồ sơ",              Amount = Sum(VoucherComponentCode.FileFee),                Count = Count(VoucherComponentCode.FileFee) },
                new { Category = VoucherComponentCode.Insurance,              CategoryName = "Bảo hiểm",               Amount = Sum(VoucherComponentCode.Insurance),              Count = Count(VoucherComponentCode.Insurance) },
                new { Category = VoucherComponentCode.LatePenalty,            CategoryName = "Phạt chậm nộp",          Amount = Sum(VoucherComponentCode.LatePenalty),            Count = Count(VoucherComponentCode.LatePenalty) },
                new { Category = VoucherComponentCode.EarlySettlementPenalty, CategoryName = "Phạt tất toán sớm",      Amount = Sum(VoucherComponentCode.EarlySettlementPenalty), Count = Count(VoucherComponentCode.EarlySettlementPenalty) },
                new { Category = VoucherComponentCode.OtherIncome,            CategoryName = "Thu khác",               Amount = Sum(VoucherComponentCode.OtherIncome),            Count = Count(VoucherComponentCode.OtherIncome) },
            };

            return categories.Where(c => c.Amount > 0).ToList();
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // Dòng tiền theo tháng
        // ───────────────────────────────────────────────────────────────────────────────
        public async Task<object> GetMonthlyCashFlow(Guid? storeId, int year)
        {
            // Giải ngân theo tháng (hợp đồng DISBURSED trong năm)
            var loansQuery = _dbContext.LoanContracts
                .Where(c => c.DisbursementDate.HasValue
                         && c.DisbursementDate.Value.Year == year);

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                loansQuery = loansQuery.Where(c => storeScopeIds.Any(id => id == c.StoreId));

            var disbursements = await loansQuery
                .Select(c => new { Month = c.DisbursementDate!.Value.Month, c.PrincipalAmount })
                .ToListAsync();

            // Thu tiền theo tháng (voucher trong năm, có allocation)
            var voucherQuery = _dbContext.CashVouchers
                .Where(v => v.BusinessDate.Year == year && v.LoanContractId != null);

            if (storeScopeIds is not null)
                voucherQuery = voucherQuery.Where(v => storeScopeIds.Any(id => id == v.StoreId));

            var vouchers = await voucherQuery
                .Select(v => new
                {
                    Month = v.BusinessDate.Month,
                    v.Amount,
                    Allocations = v.CashVoucherAllocations.Select(a => new { a.ComponentCode, a.Amount }).ToList()
                }).ToListAsync();

            var months = Enumerable.Range(1, 12).Select(m =>
            {
                var disbursed     = disbursements.Where(d => d.Month == m).Sum(d => d.PrincipalAmount);
                var mVouchers     = vouchers.Where(v => v.Month == m).ToList();
                var allAlloc      = mVouchers.SelectMany(v => v.Allocations).ToList();

                decimal CollectComp(string code) => allAlloc.Where(a => a.ComponentCode == code).Sum(a => a.Amount);

                var principal     = CollectComp(VoucherComponentCode.Principal);
                var interest      = CollectComp(VoucherComponentCode.Interest);
                var periodicFee   = CollectComp(VoucherComponentCode.PeriodicFee);
                var fileFee       = CollectComp(VoucherComponentCode.FileFee);
                var insurance     = CollectComp(VoucherComponentCode.Insurance);
                var latePenalty   = CollectComp(VoucherComponentCode.LatePenalty);
                var earlyPenalty  = CollectComp(VoucherComponentCode.EarlySettlementPenalty);

                var totalCollected = principal + interest + periodicFee + fileFee + insurance + latePenalty + earlyPenalty;
                var netIncome      = interest + periodicFee + fileFee + insurance + latePenalty + earlyPenalty; // lãi/phí = lợi nhuận

                return new
                {
                    Month          = m,
                    Disbursed      = disbursed,
                    TotalCollected = totalCollected,
                    Principal      = principal,
                    Interest       = interest,
                    PeriodicFee    = periodicFee,
                    FileFee        = fileFee,
                    Insurance      = insurance,
                    LatePenalty    = latePenalty,
                    EarlyPenalty   = earlyPenalty,
                    NetIncome      = netIncome,
                    NewLoanCount   = disbursements.Count(d => d.Month == m),
                };
            }).ToList();

            return new
            {
                Year              = year,
                TotalDisbursed    = months.Sum(m => m.Disbursed),
                TotalCollected    = months.Sum(m => m.TotalCollected),
                TotalNetIncome    = months.Sum(m => m.NetIncome),
                Months            = months,
            };
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // Tổng quan danh mục cho vay
        // ───────────────────────────────────────────────────────────────────────────────
        public async Task<object> GetLoanPortfolioSummary(Guid? storeId)
        {
            var query = _dbContext.LoanContracts.AsQueryable();

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                query = query.Where(c => storeScopeIds.Any(id => id == c.StoreId));

            var contracts = await query
                .Select(c => new
                {
                    c.StatusCode,
                    c.PrincipalAmount,
                    TotalPrincipalCollected = c.LoanRepaymentSchedules.Sum(s => (decimal?)s.PaidPrincipalAmount) ?? 0,
                    TotalInterestCollected  = c.LoanRepaymentSchedules.Sum(s => (decimal?)s.PaidInterestAmount) ?? 0,
                    TotalFeeCollected       = c.LoanRepaymentSchedules.Sum(s => (decimal?)s.PaidPeriodicFeeAmount) ?? 0,
                    TotalPenaltyCollected   = c.LoanRepaymentSchedules.Sum(s => (decimal?)s.PaidLatePenaltyAmount) ?? 0,
                    HasOverdue = c.LoanRepaymentSchedules.Any(s => s.StatusCode == "OVERDUE"),
                })
                .ToListAsync();

            var active = contracts.Where(c => c.StatusCode == "DISBURSED").ToList();

            return new
            {
                // Đếm theo trạng thái
                TotalContracts       = contracts.Count,
                DraftCount           = contracts.Count(c => c.StatusCode == "DRAFT"),
                PendingApprovalCount = contracts.Count(c => c.StatusCode == "PENDING_APPROVAL"),
                PendingDisbCount     = contracts.Count(c => c.StatusCode == "PENDING_DISBURSEMENT"),
                DisbursedCount       = contracts.Count(c => c.StatusCode == "DISBURSED"),
                BadDebtCount         = contracts.Count(c => c.StatusCode == "BAD_DEBT"),
                SettledCount         = contracts.Count(c => c.StatusCode == "SETTLED"),
                ClosedCount          = contracts.Count(c => c.StatusCode == "CLOSED"),
                CancelledCount       = contracts.Count(c => c.StatusCode == "CANCELLED"),
                // Dư nợ hiện tại
                TotalActivePortfolio = active.Sum(c => c.PrincipalAmount),
                TotalRemainingPrincipal = active.Sum(c => c.PrincipalAmount - c.TotalPrincipalCollected),
                // Hoàn—Phải trả lại bật
                OverdueContractCount = active.Count(c => c.HasOverdue),
                // Tổng đã thu
                TotalPrincipalCollected = contracts.Sum(c => c.TotalPrincipalCollected),
                TotalInterestCollected  = contracts.Sum(c => c.TotalInterestCollected),
                TotalFeeCollected       = contracts.Sum(c => c.TotalFeeCollected),
                TotalPenaltyCollected   = contracts.Sum(c => c.TotalPenaltyCollected),
                TotalIncomeCollected    = contracts.Sum(c => c.TotalInterestCollected + c.TotalFeeCollected + c.TotalPenaltyCollected),
            };
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // KPI nhân viên
        // ───────────────────────────────────────────────────────────────────────────────
        public async Task<object> GetEmployeeKpi(Guid? storeId, DateOnly fromDate, DateOnly toDate)
        {
            var fromDt = fromDate.ToDateTime(TimeOnly.MinValue);
            var toDt   = toDate.ToDateTime(TimeOnly.MaxValue);

            // Lấy tất cả nhân viên trong phạm vi
            var usersQuery = _dbContext.AppUsers
                .Where(u => u.IsActive && u.RoleCode == RoleCode.Staff);

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                usersQuery = usersQuery.Where(u => storeScopeIds.Any(id => id == u.StoreId));

            var users = await usersQuery
                .Select(u => new { u.UserId, u.FullName, u.StoreId, StoreName = u.Store != null ? u.Store.StoreName : "" })
                .ToListAsync();

            // Hợp đồng trong kỳ
            var contractsQuery = _dbContext.LoanContracts
                .Where(c => c.CreatedAt >= fromDt && c.CreatedAt <= toDt);

            if (storeScopeIds is not null)
                contractsQuery = contractsQuery.Where(c => storeScopeIds.Any(id => id == c.StoreId));

            var contracts = await contractsQuery
                .Select(c => new { c.CreatedBy, c.DisbursedBy, c.StatusCode, c.PrincipalAmount })
                .ToListAsync();

            // Phiếu thu do nhân viên tạo trong kỳ
            var vouchersQuery = _dbContext.CashVouchers
                .Where(v => v.BusinessDate >= fromDate && v.BusinessDate <= toDate
                         && v.VoucherType  == VoucherType.Receipt);

            if (storeScopeIds is not null)
                vouchersQuery = vouchersQuery.Where(v => storeScopeIds.Any(id => id == v.StoreId));

            var vouchers = await vouchersQuery
                .Select(v => new { v.CreatedBy, v.Amount })
                .ToListAsync();

            // Khách hàng mới tạo trong kỳ
            var customersQuery = _dbContext.Customers
                .Where(c => c.CreatedAt >= fromDt && c.CreatedAt <= toDt);

            if (storeScopeIds is not null)
                customersQuery = customersQuery.Where(c =>
                    _dbContext.LoanContracts.Any(lc => lc.CustomerId == c.CustomerId && storeScopeIds.Any(id => id == lc.StoreId)));

            var customers = await customersQuery
                .Select(c => new { c.CreatedBy })
                .ToListAsync();

            var badStatuses = new[] { LoanContractStatus.BadDebt, LoanContractStatus.BadDebtClosed };
            var settledStatuses = new[] { LoanContractStatus.Settled, LoanContractStatus.Closed };

            var result = users.Select(u =>
            {
                var created   = contracts.Where(c => c.CreatedBy   == u.UserId).ToList();
                var disbursed = contracts.Where(c => c.DisbursedBy == u.UserId).ToList();
                var settled   = created.Where(c => settledStatuses.Contains(c.StatusCode)).ToList();
                var badDebt   = created.Where(c => badStatuses.Contains(c.StatusCode)).ToList();
                var uVouchers = vouchers.Where(v => v.CreatedBy == u.UserId).ToList();
                var uCustomers = customers.Where(c => c.CreatedBy == u.UserId).ToList();
                int disbCount = disbursed.Count;

                return new
                {
                    u.UserId,
                    u.FullName,
                    u.StoreId,
                    u.StoreName,
                    ContractsCreated       = created.Count,
                    ContractsDisbursed     = disbCount,
                    TotalPrincipalDisbursed= disbursed.Sum(c => c.PrincipalAmount),
                    ContractsSettled       = settled.Count,
                    ContractsBadDebt       = badDebt.Count,
                    BadDebtRate            = disbCount > 0
                        ? Math.Round((decimal)badDebt.Count / disbCount * 100, 2)
                        : 0m,
                    CollectionsCount       = uVouchers.Count,
                    TotalCollected         = uVouchers.Sum(v => v.Amount),
                    CustomersCreated       = uCustomers.Count,
                };
            })
            .OrderBy(x => x.StoreName)
            .ThenByDescending(x => x.ContractsDisbursed)
            .ToList();

            return new
            {
                FromDate = fromDate,
                ToDate   = toDate,
                Employees = result,
                Summary = new
                {
                    TotalEmployees        = result.Count,
                    TotalContractsCreated = result.Sum(e => e.ContractsCreated),
                    TotalDisbursed        = result.Sum(e => e.TotalPrincipalDisbursed),
                    TotalCollected        = result.Sum(e => e.TotalCollected),
                    TotalBadDebt          = result.Sum(e => e.ContractsBadDebt),
                    TotalCustomersCreated = result.Sum(e => e.CustomersCreated),
                },
            };
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // Thống kê nâng cao về khách hàng
        // ───────────────────────────────────────────────────────────────────────────────
        public async Task<object> GetCustomerAdvancedStats(Guid? storeId, DateOnly fromDate, DateOnly toDate)
        {
            // Lấy tất cả hợp đồng đã giải ngân (không lọc theo ngày để tính toàn bộ danh mục)
            var contractsQuery = _dbContext.LoanContracts
                .Where(c => c.StatusCode != LoanContractStatus.Draft
                         && c.StatusCode != LoanContractStatus.Cancelled);

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                contractsQuery = contractsQuery.Where(c => storeScopeIds.Any(id => id == c.StoreId));

            var contracts = await contractsQuery
                .Select(c => new
                {
                    c.CustomerId,
                    c.StoreId,
                    StoreName       = c.Store.StoreName,
                    c.StatusCode,
                    c.CustomerSourceId,
                    SourceType      = c.Customer.FirstSourceType,
                    c.DisbursementDate,
                })
                .ToListAsync();

            // Khách có nhiều hơn 1 hợp đồng đã giải ngân
            var disbursedStatuses = new[]
            {
                LoanContractStatus.Disbursed, LoanContractStatus.Settled,
                LoanContractStatus.Closed, LoanContractStatus.BadDebt, LoanContractStatus.BadDebtClosed,
            };
            var disbursedContracts = contracts.Where(c => disbursedStatuses.Contains(c.StatusCode)).ToList();

            var customerLoanCounts = disbursedContracts
                .GroupBy(c => c.CustomerId)
                .ToDictionary(g => g.Key, g => g.Count());

            var returningCustomers = customerLoanCounts.Count(kv => kv.Value > 1);
            var totalDisbursedCustomers = customerLoanCounts.Count;
            var returnRate = totalDisbursedCustomers > 0
                ? Math.Round((decimal)returningCustomers / totalDisbursedCustomers * 100, 2)
                : 0m;

            // Thống kê theo chi nhánh
            var storeStats = contracts
                .GroupBy(c => new { c.StoreId, c.StoreName })
                .Select(g =>
                {
                    var sc          = g.ToList();
                    var badDebts    = sc.Count(c => c.StatusCode == LoanContractStatus.BadDebt
                                                 || c.StatusCode == LoanContractStatus.BadDebtClosed);
                    var settled     = sc.Count(c => c.StatusCode == LoanContractStatus.Settled
                                                 || c.StatusCode == LoanContractStatus.Closed);
                    var active      = sc.Count(c => c.StatusCode == LoanContractStatus.Disbursed);
                    var disbCustomers = sc.Where(c => disbursedStatuses.Contains(c.StatusCode))
                                         .Select(c => c.CustomerId).Distinct().Count();
                    var retCustomers  = sc.Where(c => disbursedStatuses.Contains(c.StatusCode))
                                         .GroupBy(c => c.CustomerId)
                                         .Count(grp => grp.Count() > 1);

                    return new
                    {
                        StoreId          = g.Key.StoreId,
                        StoreName        = g.Key.StoreName,
                        TotalContracts   = sc.Count,
                        ActiveContracts  = active,
                        SettledContracts = settled,
                        BadDebtContracts = badDebts,
                        BadDebtRate      = sc.Count > 0
                            ? Math.Round((decimal)badDebts / sc.Count * 100, 2) : 0m,
                        TotalCustomers   = disbCustomers,
                        ReturningCustomers = retCustomers,
                        ReturnRate       = disbCustomers > 0
                            ? Math.Round((decimal)retCustomers / disbCustomers * 100, 2) : 0m,
                    };
                })
                .OrderByDescending(s => s.BadDebtRate)
                .ToList();

            // Thống kê theo luồng khách
            var bySource = disbursedContracts
                .GroupBy(c => c.SourceType ?? SourceType.VangLai)
                .Select(g => new
                {
                    SourceType    = g.Key,
                    SourceName    = g.Key == SourceType.Ctv ? "CTV" : g.Key == SourceType.KhachCu ? "Khách cũ" : "Vãng lai",
                    ContractCount = g.Count(),
                    CustomerCount = g.Select(c => c.CustomerId).Distinct().Count(),
                    BadDebtCount  = g.Count(c => c.StatusCode == LoanContractStatus.BadDebt
                                              || c.StatusCode == LoanContractStatus.BadDebtClosed),
                })
                .OrderByDescending(s => s.ContractCount)
                .ToList();

            // Chi nhánh có khách quay lại nhiều nhất
            var topReturnStore = storeStats.OrderByDescending(s => s.ReturnRate).FirstOrDefault();

            // Chi nhánh có nhiều nợ xấu nhất
            var topBadDebtStore = storeStats.OrderByDescending(s => s.BadDebtRate).FirstOrDefault();

            return new
            {
                FromDate             = fromDate,
                ToDate               = toDate,
                TotalContracts       = contracts.Count,
                ActiveContracts      = contracts.Count(c => c.StatusCode == LoanContractStatus.Disbursed),
                SettledContracts     = contracts.Count(c => c.StatusCode == LoanContractStatus.Settled || c.StatusCode == LoanContractStatus.Closed),
                BadDebtContracts     = contracts.Count(c => c.StatusCode == LoanContractStatus.BadDebt || c.StatusCode == LoanContractStatus.BadDebtClosed),
                TotalCustomers       = totalDisbursedCustomers,
                ReturningCustomers   = returningCustomers,
                ReturnRate           = returnRate,
                TopReturnStoreName   = topReturnStore?.StoreName,
                TopReturnStoreRate   = topReturnStore?.ReturnRate,
                TopBadDebtStoreName  = topBadDebtStore?.StoreName,
                TopBadDebtStoreRate  = topBadDebtStore?.BadDebtRate,
                ByStore              = storeStats,
                BySource             = bySource,
            };
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // Báo cáo tài sản đảm bảo
        // ───────────────────────────────────────────────────────────────────────────────
        public async Task<object> GetCollateralReport(Guid? storeId, DateOnly fromDate, DateOnly toDate)
        {
            var fromDt = fromDate.ToDateTime(TimeOnly.MinValue);
            var toDt   = toDate.ToDateTime(TimeOnly.MaxValue);

            // Tất cả tài sản đảm bảo trong kỳ
            var collateralQuery = _dbContext.LoanCollaterals
                .Where(lc => lc.CreatedAt >= fromDt && lc.CreatedAt <= toDt);

            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                collateralQuery = collateralQuery.Where(lc => storeScopeIds.Any(id => id == lc.LoanContract.StoreId));

            var collaterals = await collateralQuery
                .Select(lc => new
                {
                    lc.CollateralType,
                    lc.EstimatedValue,
                    lc.LoanContractId,
                    ContractStatus = lc.LoanContract.StatusCode,
                    StoreId        = lc.LoanContract.StoreId,
                })
                .ToListAsync();

            // Phiếu thu hồi nợ xấu trong kỳ (để tính recovery amount theo tài sản)
            var recoveryQuery = _dbContext.CashVouchers
                .Where(v => v.BusinessDate >= fromDate && v.BusinessDate <= toDate
                         && v.ReasonCode   == VoucherReasonCode.BadDebtRecovery);

            if (storeScopeIds is not null)
                recoveryQuery = recoveryQuery.Where(v => storeScopeIds.Any(id => id == v.StoreId));

            var recoveries = await recoveryQuery
                .Select(v => new { v.BusinessDate.Month, v.BusinessDate.Year, v.Amount })
                .ToListAsync();

            var badStatuses = new[] { LoanContractStatus.BadDebt, LoanContractStatus.BadDebtClosed };

            // Thống kê theo loại tài sản
            var byType = collaterals
                .GroupBy(c => string.IsNullOrWhiteSpace(c.CollateralType) ? "Khác" : c.CollateralType)
                .Select(g =>
                {
                    var items    = g.ToList();
                    var total    = items.Count;
                    var badDebt  = items.Count(c => badStatuses.Contains(c.ContractStatus));
                    var totalVal = items.Sum(c => c.EstimatedValue ?? 0);

                    return new
                    {
                        CollateralType   = g.Key,
                        TotalCount       = total,
                        TotalValue       = totalVal,
                        AvgValue         = total > 0 ? Math.Round(totalVal / total, 0) : 0m,
                        BadDebtCount     = badDebt,
                        BadDebtRate      = total > 0
                            ? Math.Round((decimal)badDebt / total * 100, 2) : 0m,
                    };
                })
                .OrderByDescending(x => x.TotalCount)
                .ToList();

            // Thu hồi theo tháng (tổng tất cả)
            var recoveryByMonth = recoveries
                .GroupBy(r => new { r.Year, r.Month })
                .Select(g => new
                {
                    YearMonth       = $"{g.Key.Year}-{g.Key.Month:D2}",
                    RecoveredAmount = g.Sum(r => r.Amount),
                })
                .OrderBy(x => x.YearMonth)
                .ToList();

            // Thu hồi theo năm
            var recoveryByYear = recoveries
                .GroupBy(r => r.Year)
                .Select(g => new
                {
                    Year            = g.Key,
                    RecoveredAmount = g.Sum(r => r.Amount),
                    MonthCount      = g.Select(r => r.Month).Distinct().Count(),
                    AvgPerMonth     = g.Select(r => r.Month).Distinct().Count() > 0
                        ? Math.Round(g.Sum(r => r.Amount) / g.Select(r => r.Month).Distinct().Count(), 0)
                        : 0m,
                })
                .OrderBy(x => x.Year)
                .ToList();

            var totalCollaterals = collaterals.Count;
            var totalBadDebt = collaterals.Count(c => badStatuses.Contains(c.ContractStatus));

            return new
            {
                FromDate             = fromDate,
                ToDate               = toDate,
                TotalCollaterals     = totalCollaterals,
                TotalEstimatedValue  = collaterals.Sum(c => c.EstimatedValue ?? 0),
                TotalBadDebt         = totalBadDebt,
                OverallBadDebtRate   = totalCollaterals > 0
                    ? Math.Round((decimal)totalBadDebt / totalCollaterals * 100, 2) : 0m,
                TotalRecovered       = recoveries.Sum(r => r.Amount),
                ByCollateralType     = byType,
                RecoveryByMonth      = recoveryByMonth,
                RecoveryByYear       = recoveryByYear,
            };
        }
    }
}
