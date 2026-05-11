using CrediFlow.API.Models;
using CrediFlow.API.Utils;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace CrediFlow.API.Services
{
    public interface ILoanContractService : IBaseService<LoanContract>
    {
        /// <summary>Tạo mới hoặc cập nhật hợp đồng vay.</summary>
        Task<LoanContract> Save(CULoanContractModel model);

        /// <summary>Lấy danh sách hợp đồng theo quyền.</summary>
        Task<IList<LoanContractListItem>> GetAlls();

        /// <summary>Tìm kiếm hợp đồng với phân trang và sắp xếp.</summary>
        Task<object> SearchLoanContract(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc, string? statusCode = null, string? dateFilterType = null, DateOnly? fromDate = null, DateOnly? toDate = null, List<Guid>? filterStoreIds = null);

        /// <summary>Tính PMT và lịch trả nợ dự tính trước khi tạo hợp đồng.</summary>
        LoanCalculationResult Calculate(LoanCalculateRequest request);

        /// <summary>Hủy hợp đồng vay (không cho sửa – nếu sai thì hủy và tạo mới).</summary>
        Task<LoanContract> Cancel(Guid loanContractId, string cancellationReason);

        /// <summary>Lấy lịch trả nợ của hợp đồng đã tạo.</summary>
        Task<IList<LoanRepaymentSchedule>> GetRepaymentSchedule(Guid loanContractId);

        /// <summary>Danh sách kỳ đang chậm nộp (từ DB view v_overdue_schedule).</summary>
        Task<IList<VOverdueSchedule>> GetOverdueList(int? minDaysOverdue, int? maxDaysOverdue);

        /// <summary>
        /// Chuyển trạng thái hợp đồng theo luồng nghiệp vụ.
        /// Ma trận hợp lệ:
        ///   DRAFT               → PENDING_APPROVAL | CANCELLED
        ///   PENDING_APPROVAL    → PENDING_DISBURSEMENT (duyệt) | DRAFT (trả lại) | CANCELLED
        ///   PENDING_DISBURSEMENT→ DISBURSED | CANCELLED
        ///   DISBURSED           → PENDING_BAD_DEBT (nhân viên đề xuất) | BAD_DEBT (manager/admin trực tiếp) | CLOSED (admin only; SETTLED qua LoanSettlement)
        ///   PENDING_BAD_DEBT    → BAD_DEBT (duyệt) | DISBURSED (từ chối)
        ///   BAD_DEBT            → BAD_DEBT_CLOSED
        ///   SETTLED/CLOSED/CANCELLED → terminal, không chuyển tiếp
        /// </summary>
        Task<LoanContract> ChangeStatus(Guid loanContractId, string toStatus, string? reason);

        /// <summary>Lịch sử thay đổi trạng thái hợp đồng (sắp xếp theo thời gian).</summary>
        Task<IList<LoanStatusHistory>> GetStatusHistory(Guid loanContractId);

        /// <summary>Lịch sử thao tác hợp đồng (AuditLogs).</summary>
        Task<IList<AuditLogDto>> GetAuditLogs(Guid loanContractId);

        /// <summary>Tóm tắt tài chính của hợp đồng: tổng giải ngân, đã thu, còn lại, lãi/phí/phạt.</summary>
        Task<object> GetFinancialSummary(Guid loanContractId);

        /// <summary>Chuyển giao hợp đồng cho nhân viên khác phụ trách (chỉ Manager/Admin).</summary>
        Task<LoanContract> AssignLoanContract(Guid loanContractId, Guid targetUserId);

        /// <summary>Tự động tính lại PMT cho các kỳ tương lai nếu kỳ vừa qua đóng thiếu gốc.</summary>
        Task<bool> SyncScheduleForShortPaymentAsync(Guid loanContractId);

        /// <summary>Tính toán lại lịch trả nợ cho tất cả các hợp đồng trả góp chưa có bất kỳ thanh toán nào.</summary>
        Task<int> RecalculateAllPendingSchedulesAsync();
    }

    // DTO cho calculator
    public class LoanCalculateRequest
    {
        public string ContractType { get; set; } = LoanContractType.Installment;
        public decimal PrincipalAmount { get; set; }
        /// <summary>Phí bảo hiểm cộng vào gốc vay. P hiệu dụng = PrincipalAmount + InsuranceAmountSnapshot.</summary>
        public decimal InsuranceAmountSnapshot { get; set; }
        public int TermMonths { get; set; }
        public decimal InterestRateMonthly { get; set; }   // %/tháng
        public decimal QlkvRateMonthly { get; set; }        // Phí QLKV %/tháng
        public decimal QltsRateMonthly { get; set; }        // Phí QLTS %/tháng
        public decimal FixedMonthlyFeeAmount { get; set; }  // Phí cố định thu theo tháng (VNĐ)
        public decimal PawnInterestAmountPerMillionPerDay { get; set; }
        public decimal PawnFeeAmountPerMillionPerDay { get; set; }
        public short PawnPeriodDays { get; set; } = 10;
        public DateOnly? DisbursementDate { get; set; }   // nếu có thì tính ngày thực tế
    }

    public class LoanCalculationResult
    {
        public decimal InstallmentAmount { get; set; }   // số tiền trả đều mỗi kỳ (PMT)
        public decimal TotalPayment { get; set; }
        public decimal TotalInterest { get; set; }
        public decimal TotalQlkv { get; set; }
        public decimal TotalQlts { get; set; }
        public decimal TotalFixedMonthlyFee { get; set; }
        /// <summary>Tổng phí định kỳ (QLKV + QLTS + phí cố định/tháng).</summary>
        public decimal TotalPeriodicFee => TotalQlkv + TotalQlts + TotalFixedMonthlyFee;
        public decimal TotalPrincipal { get; set; }
        public List<LoanSchedulePreviewItem> Schedule { get; set; } = new();
    }

    public class LoanSchedulePreviewItem
    {
        public int PeriodNo { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public int DayCount { get; set; }
        public decimal OpeningPrincipal { get; set; }
        public decimal DueInterest { get; set; }
        /// <summary>Phí QLKV kỳ này.</summary>
        public decimal DueQlkv { get; set; }
        /// <summary>Phí cố định thu theo tháng.</summary>
        public decimal DueFixedMonthlyFee { get; set; }
        /// <summary>Phí QLTS kỳ này.</summary>
        public decimal DueQlts { get; set; }
        /// <summary>Tổng phí định kỳ (QLKV + QLTS + Phí cố định/tháng).</summary>
        public decimal DuePeriodicFee => DueQlkv + DueQlts + DueFixedMonthlyFee;
        public decimal DuePrincipal { get; set; }
        public decimal Installment { get; set; }
        public decimal ClosingPrincipal { get; set; }
    }

    /// <summary>DTO dùng cho danh sách hợp đồng (tránh Include, join qua Select).</summary>
    public class LoanContractListItem
    {
        public Guid LoanContractId { get; set; }
        public string ContractNo { get; set; } = null!;
        public Guid StoreId { get; set; }
        public Guid CustomerId { get; set; }
        public string ContractType { get; set; } = LoanContractType.Installment;
        public Guid? LoanProductId { get; set; }
        public Guid? PreviousLoanContractId { get; set; }
        public string StatusCode { get; set; } = null!;
        public DateOnly ApplicationDate { get; set; }
        public DateOnly? ApprovedDate { get; set; }
        public DateOnly? DisbursementDate { get; set; }
        public DateOnly? FirstDueDate { get; set; }
        public DateOnly? MaturityDate { get; set; }
        public int TermMonths { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal NetDisbursedAmount { get; set; }
        public decimal InterestRateMonthlySnapshot { get; set; }
        public decimal QlkvRateMonthlySnapshot { get; set; }
        public decimal QltsRateMonthlySnapshot { get; set; }
        public decimal FixedMonthlyFeeAmountSnapshot { get; set; }
        public decimal FileFeeAmountSnapshot { get; set; }
        public decimal InsuranceAmountSnapshot { get; set; }
        public decimal PawnInterestAmountPerMillionPerDaySnapshot { get; set; }
        public decimal PawnFeeAmountPerMillionPerDaySnapshot { get; set; }
        public short PawnPeriodDaysSnapshot { get; set; }
        public decimal InsuranceDiscountRateSnapshot { get; set; }
        public decimal EarlySettlementPenaltyRateSnapshot { get; set; }
        public decimal LatePaymentPenaltyRateSnapshot { get; set; }
        public short LatePaymentStartDaySnapshot { get; set; }
        public short BadDebtStartDaySnapshot { get; set; }
        public List<short> WarningDaysSnapshot { get; set; } = null!;
        public DateTime? CancelledAt { get; set; }
        public Guid? CancelledBy { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? SettledAt { get; set; }
        public string? Note { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? ApprovedBy { get; set; }
        public Guid? DisbursedBy { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        // Các field join từ bảng liên quan
        public string? CustomerName { get; set; }
        public string? CustomerNationalId { get; set; }
        public string? LoanProductName { get; set; }
        public Guid?   CustomerSourceId   { get; set; }
        public string? CustomerSourceName { get; set; }
    }

    public class LoanContractService : BaseService<LoanContract, CrediflowContext>, ILoanContractService
    {
        public LoanContractService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<LoanContractListItem>> GetAlls()
        {
            var query = DbContext.LoanContracts.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(c => storeScopeIds.Contains(c.StoreId));

            // Staff chỉ thấy hợp đồng mình tạo hoặc được gán phụ trách
            if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
            {
                var staffId = CommonLib.GetGUID(User.UserId);
                query = query.Where(c => c.CreatedBy == staffId || c.AssignedToUserId == staffId);
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new LoanContractListItem
                {
                    LoanContractId = c.LoanContractId,
                    ContractNo = c.ContractNo,
                    StoreId = c.StoreId,
                    CustomerId = c.CustomerId,
                    ContractType = c.ContractType,
                    LoanProductId = c.LoanProductId,
                    PreviousLoanContractId = c.PreviousLoanContractId,
                    StatusCode = c.StatusCode,
                    ApplicationDate = c.ApplicationDate,
                    ApprovedDate = c.ApprovedDate,
                    DisbursementDate = c.DisbursementDate,
                    FirstDueDate = c.FirstDueDate,
                    MaturityDate = c.MaturityDate,
                    TermMonths = c.TermMonths,
                    PrincipalAmount = c.PrincipalAmount,
                    NetDisbursedAmount = c.NetDisbursedAmount,
                    InterestRateMonthlySnapshot = c.InterestRateMonthlySnapshot,
                    QlkvRateMonthlySnapshot = c.QlkvRateMonthlySnapshot,
                    QltsRateMonthlySnapshot = c.QltsRateMonthlySnapshot,
                    FixedMonthlyFeeAmountSnapshot = c.FixedMonthlyFeeAmountSnapshot,
                    FileFeeAmountSnapshot = c.FileFeeAmountSnapshot,
                    InsuranceAmountSnapshot = c.InsuranceAmountSnapshot,
                    InsuranceDiscountRateSnapshot = c.InsuranceDiscountRateSnapshot,
                    EarlySettlementPenaltyRateSnapshot = c.EarlySettlementPenaltyRateSnapshot,
                    LatePaymentPenaltyRateSnapshot = c.LatePaymentPenaltyRateSnapshot,
                    LatePaymentStartDaySnapshot = c.LatePaymentStartDaySnapshot,
                    BadDebtStartDaySnapshot = c.BadDebtStartDaySnapshot,
                    WarningDaysSnapshot = c.WarningDaysSnapshot,
                    CancelledAt = c.CancelledAt,
                    CancelledBy = c.CancelledBy,
                    CancellationReason = c.CancellationReason,
                    SettledAt = c.SettledAt,
                    Note = c.Note,
                    CreatedBy = c.CreatedBy,
                    ApprovedBy = c.ApprovedBy,
                    DisbursedBy = c.DisbursedBy,
                    AssignedToUserId = c.AssignedToUserId,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    // Join nhẹ qua navigation – EF dịch thành LEFT JOIN, không load toàn bộ entity
                    CustomerName = c.Customer.FullName,
                    CustomerNationalId = c.Customer.NationalId,
                    LoanProductName = c.LoanProduct != null ? c.LoanProduct.ProductName : null,
                    CustomerSourceId   = c.CustomerSourceId,
                    CustomerSourceName = c.CustomerSource != null ? c.CustomerSource.SourceName : null,
                })
                .ToListAsync();
        }

        public async Task<LoanContract> Save(CULoanContractModel model)
        {
            bool isCreate = model.LoanContractId == null || model.LoanContractId == Guid.Empty;
            LoanContract obj;

            if (isCreate)
            {
                // Kiểm tra quyền tạo hợp đồng từ DB (dynamic permissions)
                if (!await PermissionChecker.HasPermissionAsync(
                        DbContext, CachingHelper, CommonLib.GetGUID(User.UserId) ?? Guid.Empty,
                        User.RoleCode, "LOAN_CREATE"))
                    throw new UnauthorizedAccessException(
                        "Bạn không có quyền tạo hợp đồng vay. Vui lòng liên hệ quản trị viên.");

                // Kiểm tra InitialStatus hợp lệ
                var allowedInitialStatuses = new[] {
                    LoanContractStatus.Draft,
                    LoanContractStatus.PendingApproval,
                    LoanContractStatus.Disbursed,   // nhập hậu kỳ
                };
                string initialStatus = model.InitialStatus ?? LoanContractStatus.Draft;
                string contractType = string.IsNullOrWhiteSpace(model.ContractType)
                    ? LoanContractType.Installment
                    : model.ContractType.Trim().ToUpperInvariant();
                if (contractType != LoanContractType.Installment && contractType != LoanContractType.Pawn)
                    throw new ArgumentException($"Loại hợp đồng không hợp lệ: '{contractType}'.");
                if (!allowedInitialStatuses.Contains(initialStatus))
                    throw new ArgumentException(
                        $"Trạng thái khởi tạo không hợp lệ: '{initialStatus}'.");

                // Chỉ Manager/Admin mới được tạo thẳng trạng thái DISBURSED
                if (initialStatus == LoanContractStatus.Disbursed && !User.IsStoreManager && !User.IsRegionalManager && !User.IsAdmin)
                    throw new UnauthorizedAccessException(
                        "Chỉ quản lý cửa hàng hoặc admin mới có thể nhập hợp đồng đã giải ngân trực tiếp.");

                var creatorId = CommonLib.GetGUID(User.UserId);

                // Chặn duplicate: cùng KH + chi nhánh + số tiền + người tạo trong 30s
                var cutoff = DateTime.Now.AddSeconds(-30);
                var hasDuplicate = await DbContext.LoanContracts
                    .AnyAsync(c => c.CustomerId == model.CustomerId
                        && c.StoreId == model.StoreId
                        && c.PrincipalAmount == model.PrincipalAmount
                        && c.CreatedAt >= cutoff
                        && c.CreatedBy == creatorId);
                if (hasDuplicate)
                    throw new InvalidOperationException(
                        "Hợp đồng tương tự đã được tạo trong vòng 30 giây trước. " +
                        "Vui lòng kiểm tra lại danh sách hợp đồng.");

                // Lấy chính sách đang hiệu lực: ưu tiên chính sách cửa hàng, fallback về chính sách toàn cục
                var today = DateOnly.FromDateTime(DateTime.Today);
                var activePolicy = await DbContext.PolicySettings
                    .Where(p => (p.Stores.Any(s => s.StoreId == model.StoreId) || !p.Stores.Any())
                             && p.EffectiveFrom <= today
                             && (p.EffectiveTo == null || p.EffectiveTo >= today))
                    .OrderByDescending(p => p.Stores.Any(s => s.StoreId == model.StoreId))  // store-specific trước
                    .ThenByDescending(p => p.EffectiveFrom)
                    .FirstOrDefaultAsync();

                obj = new LoanContract
                {
                    LoanContractId = Guid.CreateVersion7(),
                    // Với backdated disbursement, tạo trước ở PENDING_DISBURSEMENT rồi gọi ChangeStatus
                    StatusCode = initialStatus == LoanContractStatus.Disbursed
                        ? LoanContractStatus.PendingDisbursement
                        : initialStatus,
                    ContractType = contractType,
                    ContractNo = await GenerateContractNoAsync(model.StoreId),
                    CreatedBy = creatorId,
                    AssignedToUserId = creatorId,   // mặc định phụ trách = người tạo
                    WarningDaysSnapshot = activePolicy?.WarningDays ?? new List<short> { 5, 10, 15 },
                };

                // Ghi đè snapshot chính sách từ PolicySetting hiện hành (nếu có)
                if (activePolicy != null)
                {
                    model.EarlySettlementPenaltyRateSnapshot  = activePolicy.EarlySettlementPenaltyRate;
                    model.LatePaymentPenaltyRateSnapshot      = activePolicy.LatePaymentPenaltyRate;
                    model.LatePaymentStartDaySnapshot         = activePolicy.LatePaymentStartDay;
                    model.BadDebtStartDaySnapshot             = activePolicy.BadDebtStartDay;
                    model.InsuranceDiscountRateSnapshot       = activePolicy.InsuranceDiscountRate;
                }

                DbContext.LoanContracts.Add(obj);
            }
            else
            {
                obj = await DbContext.LoanContracts.FindAsync(model.LoanContractId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy hợp đồng với Id = {model.LoanContractId}");

                // Chỉ cho sửa khi hợp đồng còn ở trạng thái DRAFT
                // Sau khi duyệt / giải ngân: không được sửa, phải hủy và tạo mới
                if (obj.StatusCode != LoanContractStatus.Draft)
                    throw new InvalidOperationException(
                        $"Không thể sửa hợp đồng ở trạng thái '{obj.StatusCode}'. " +
                        "Nếu cần thay đổi, vui lòng hủy và tạo hợp đồng mới.");

                // Staff chỉ được sửa hợp đồng mình tạo hoặc được gán phụ trách
                if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
                {
                    var staffId = CommonLib.GetGUID(User.UserId);
                    if (obj.CreatedBy != staffId && obj.AssignedToUserId != staffId)
                        throw new UnauthorizedAccessException(
                            "Bạn chỉ có thể chỉnh sửa hợp đồng mà bạn đang phụ trách.");
                }
            }

            if (!User.IsAdmin)
            {
                var storeScopeIds = GetStoreScopeIds();
                if (storeScopeIds is not null && !storeScopeIds.Any(id => id == model.StoreId))
                    throw new UnauthorizedAccessException("Bạn không có quyền thao tác với chi nhánh ngoài phạm vi được phân công.");
            }

            obj.StoreId = model.StoreId;
            obj.CustomerId = model.CustomerId;
            obj.ContractType = string.IsNullOrWhiteSpace(model.ContractType)
                ? obj.ContractType
                : model.ContractType.Trim().ToUpperInvariant();
            obj.LoanProductId = model.LoanProductId ?? obj.LoanProductId;
            obj.PreviousLoanContractId = model.PreviousLoanContractId ?? obj.PreviousLoanContractId;
            obj.ApplicationDate = model.ApplicationDate;
            obj.TermMonths = model.TermMonths;
            obj.PrincipalAmount = model.PrincipalAmount;
            obj.NetDisbursedAmount = model.NetDisbursedAmount;
            obj.InterestRateMonthlySnapshot = model.InterestRateMonthlySnapshot;
            obj.QlkvRateMonthlySnapshot = model.QlkvRateMonthlySnapshot;
            obj.QltsRateMonthlySnapshot = model.QltsRateMonthlySnapshot;
            obj.FixedMonthlyFeeAmountSnapshot = model.FixedMonthlyFeeAmountSnapshot;
            obj.FileFeeAmountSnapshot = model.FileFeeAmountSnapshot;
            obj.InsuranceAmountSnapshot = model.InsuranceAmountSnapshot;
            obj.PawnInterestAmountPerMillionPerDaySnapshot = model.PawnInterestAmountPerMillionPerDaySnapshot;
            obj.PawnFeeAmountPerMillionPerDaySnapshot = model.PawnFeeAmountPerMillionPerDaySnapshot;
            obj.PawnPeriodDaysSnapshot = model.PawnPeriodDaysSnapshot <= 0 ? (short)10 : model.PawnPeriodDaysSnapshot;
            obj.InsuranceDiscountRateSnapshot = model.InsuranceDiscountRateSnapshot;
            obj.EarlySettlementPenaltyRateSnapshot = model.EarlySettlementPenaltyRateSnapshot;
            obj.LatePaymentPenaltyRateSnapshot = model.LatePaymentPenaltyRateSnapshot;
            obj.LatePaymentStartDaySnapshot = model.LatePaymentStartDaySnapshot;
            obj.BadDebtStartDaySnapshot = model.BadDebtStartDaySnapshot;
            obj.Note             = model.Note ?? obj.Note;
            obj.CustomerSourceId = model.CustomerSourceId;
            // Với backdated disbursement: giữ PENDING_DISBURSEMENT để ChangeStatus xử lý chuyển sang DISBURSED
            bool isBackdatedDisburse = isCreate && model.InitialStatus == LoanContractStatus.Disbursed;
            obj.StatusCode = isBackdatedDisburse
                ? LoanContractStatus.PendingDisbursement
                : (model.InitialStatus ?? obj.StatusCode);

            await DbContext.SaveChangesAsync();

            // Xử lý giải ngân hậu kỳ: set ngày giải ngân thực tế rồi gọi ChangeStatus
            if (isBackdatedDisburse)
            {
                obj.DisbursementDate = model.BackdatedDisbursedDate;
                await DbContext.SaveChangesAsync();   // lưu ngày giải ngân trước khi ChangeStatus
                await ChangeStatus(obj.LoanContractId, LoanContractStatus.Disbursed,
                    "Nhập hệ thống – hợp đồng đã giải ngân trước khi vào hệ thống");
            }

            return obj;
        }

        private async Task<string> GenerateContractNoAsync(Guid storeId)
        {
            var storeName = await DbContext.Stores
                .Where(s => s.StoreId == storeId)
                .Select(s => s.StoreName)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(storeName))
                throw new KeyNotFoundException($"Không tìm thấy chi nhánh với Id = {storeId}");

            var year2 = (DateTime.Now.Year % 100).ToString("00");
            var storeToken = BuildStoreToken(storeName);
            var prefix = $"{year2}{storeToken}-HD-";

            var usedCodes = await DbContext.LoanContracts
                .Where(c => c.StoreId == storeId && c.ContractNo.StartsWith(prefix))
                .Select(c => c.ContractNo)
                .ToListAsync();

            var maxSeq = 0;
            foreach (var code in usedCodes)
            {
                var seqText = code[prefix.Length..];
                if (seqText.Length != 4 || !int.TryParse(seqText, out var seq)) continue;
                if (seq > maxSeq) maxSeq = seq;
            }

            return $"{prefix}{(maxSeq + 1):0000}";
        }

        private static string BuildStoreToken(string storeName)
        {
            var normalized = storeName
                .Normalize(NormalizationForm.FormD)
                .Replace('đ', 'd')
                .Replace('Đ', 'D');

            var ascii = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) ascii.Append(char.ToUpperInvariant(ch));
                else ascii.Append(' ');
            }

            var token = string.Concat(
                ascii.ToString()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part[0])
            );

            return string.IsNullOrWhiteSpace(token) ? "CN" : token;
        }

        public async Task<object> SearchLoanContract(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc, string? statusCode = null, string? dateFilterType = null, DateOnly? fromDate = null, DateOnly? toDate = null, List<Guid>? filterStoreIds = null)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;


            var query = DbContext.LoanContracts
                .AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(c => storeScopeIds.Contains(c.StoreId));

            // Staff chỉ thấy hợp đồng mình tạo hoặc được gán phụ trách
            if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
            {
                var staffId = CommonLib.GetGUID(User.UserId);
                query = query.Where(c => c.CreatedBy == staffId || c.AssignedToUserId == staffId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(c =>
                    c.ContractNo.ToLower().Contains(keyword) ||
                    c.Customer.FullName.ToLower().Contains(keyword) ||
                    c.Customer.NationalId.ToLower().Contains(keyword) ||
                    (c.Note != null && c.Note.ToLower().Contains(keyword)));
            }

            // Lọc theo trạng thái hợp đồng
            if (!string.IsNullOrWhiteSpace(statusCode))
                query = query.Where(c => c.StatusCode == statusCode);

            // Lọc theo khoảng ngày
            if (fromDate.HasValue || toDate.HasValue)
            {
                string dt = dateFilterType?.Trim().ToUpperInvariant() ?? "APPLICATION_DATE";
                if (dt == "DISBURSEMENT_DATE" || dt == "DISBURSEDDATE")
                {
                    if (fromDate.HasValue) query = query.Where(c => c.DisbursementDate >= fromDate);
                    if (toDate.HasValue)   query = query.Where(c => c.DisbursementDate <= toDate);
                }
                else
                {
                    if (fromDate.HasValue) query = query.Where(c => c.ApplicationDate >= fromDate);
                    if (toDate.HasValue)   query = query.Where(c => c.ApplicationDate <= toDate);
                }
            }

            // Lọc theo danh sách cửa hàng (Admin only — đã kiểm tra quyền ở controller)
            if (filterStoreIds != null && filterStoreIds.Any())
                query = query.Where(c => filterStoreIds.Contains(c.StoreId));

            var aggregates = await query
                .GroupBy(c => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    SumPrincipal = g.Sum(c => c.PrincipalAmount),
                    SumInsurance = g.Sum(c => c.InsuranceAmountSnapshot),
                    SumFileFee   = g.Sum(c => c.FileFeeAmountSnapshot)
                })
                .FirstOrDefaultAsync();

            int total = aggregates?.Total ?? 0;
            decimal sumPrincipal = aggregates?.SumPrincipal ?? 0;
            decimal sumInsurance = aggregates?.SumInsurance ?? 0;
            decimal sumFileFee   = aggregates?.SumFileFee ?? 0;

            var sorted = (sortBy?.Trim().ToLower() ?? "contractno") switch
            {
                "contractno" => sortDesc ? query.OrderByDescending(c => c.ContractNo).ThenByDescending(c => c.CreatedAt) : query.OrderBy(c => c.ContractNo).ThenByDescending(c => c.CreatedAt),
                "applicationdate" => sortDesc ? query.OrderByDescending(c => c.ApplicationDate).ThenByDescending(c => c.CreatedAt) : query.OrderBy(c => c.ApplicationDate).ThenByDescending(c => c.CreatedAt),
                "statuscode" => sortDesc ? query.OrderByDescending(c => c.StatusCode).ThenByDescending(c => c.CreatedAt) : query.OrderBy(c => c.StatusCode).ThenByDescending(c => c.CreatedAt),
                "principalamount" => sortDesc ? query.OrderByDescending(c => c.PrincipalAmount).ThenByDescending(c => c.CreatedAt) : query.OrderBy(c => c.PrincipalAmount).ThenByDescending(c => c.CreatedAt),
                "updatedat" => sortDesc ? query.OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.CreatedAt) : query.OrderBy(c => c.UpdatedAt).ThenByDescending(c => c.CreatedAt),
                _ => sortDesc ? query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.LoanContractId) : query.OrderBy(c => c.CreatedAt).ThenBy(c => c.LoanContractId)
            };

            var items = await sorted
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.LoanContractId,
                    c.ContractNo,
                    c.StoreId,
                    c.Store.StoreName,
                    c.CustomerId,
                    CustomerName = c.Customer.FullName,
                    c.Customer.CustomerCode,
                    CustomerPhone = c.Customer.Phone,
                    c.LoanProductId,
                    c.StatusCode,
                    c.ApplicationDate,
                    c.ApprovedDate,
                    DisbursedDate = c.DisbursementDate,
                    c.FirstDueDate,
                    c.MaturityDate,
                    c.TermMonths,
                    c.PrincipalAmount,
                    c.NetDisbursedAmount,
                    c.InterestRateMonthlySnapshot,
                    c.QlkvRateMonthlySnapshot,
                    c.QltsRateMonthlySnapshot,
                    c.FixedMonthlyFeeAmountSnapshot,
                    c.ContractType,
                    c.FileFeeAmountSnapshot,
                    c.InsuranceAmountSnapshot,
                    c.EarlySettlementPenaltyRateSnapshot,
                    c.LatePaymentPenaltyRateSnapshot,
                    c.LatePaymentStartDaySnapshot,
                    c.BadDebtStartDaySnapshot,
                    c.Note,
                    c.CreatedBy,
                    c.AssignedToUserId,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.SettledAt,
                    c.CancelledAt,
                    c.CustomerSourceId,
                    CustomerSourceName = c.CustomerSource != null ? c.CustomerSource.SourceName : null,
                    c.PawnInterestAmountPerMillionPerDaySnapshot,
                    c.PawnFeeAmountPerMillionPerDaySnapshot
                })
                .ToListAsync();

            return new
            {
                TotalCount = total,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Items = items,
                TotalPrincipal = sumPrincipal,
                TotalInsurance = sumInsurance,
                TotalFileFee = sumFileFee
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Calculate – tính PMT và lịch trả nợ dự tính
        // ──────────────────────────────────────────────────────────────────────

        public LoanCalculationResult Calculate(LoanCalculateRequest request)
        {
            var contractType = string.IsNullOrWhiteSpace(request.ContractType)
                ? LoanContractType.Installment
                : request.ContractType.Trim().ToUpperInvariant();

            if (contractType == LoanContractType.Pawn)
            {
                decimal principal = request.PrincipalAmount + request.InsuranceAmountSnapshot;
                int periodCount = request.TermMonths;
                short periodDays = request.PawnPeriodDays <= 0 ? (short)10 : request.PawnPeriodDays;
                decimal interestRatePerMillionPerDay = request.PawnInterestAmountPerMillionPerDay;
                decimal feeRatePerMillionPerDay = request.PawnFeeAmountPerMillionPerDay;
                DateOnly? pawnDisbursementDate = request.DisbursementDate;

                var pawnResult = new LoanCalculationResult
                {
                    InstallmentAmount = 0,
                    TotalPrincipal = 0,
                };

                decimal totalInterest = 0;
                decimal totalQlts = 0;

                for (int i = 1; i <= periodCount; i++)
                {
                    var fromDate = pawnDisbursementDate?.AddDays((i - 1) * periodDays);
                    var toDate = pawnDisbursementDate?.AddDays(i * periodDays);
                    var dayCount = periodDays;

                    decimal interest = Math.Round(principal * interestRatePerMillionPerDay * dayCount / 1_000_000m, 0);
                    decimal qlts = Math.Round(principal * feeRatePerMillionPerDay * dayCount / 1_000_000m, 0);
                    decimal total = interest + qlts;

                    pawnResult.Schedule.Add(new LoanSchedulePreviewItem
                    {
                        PeriodNo = i,
                        FromDate = fromDate,
                        ToDate = toDate,
                        DayCount = dayCount,
                        OpeningPrincipal = principal,
                        DueInterest = interest,
                        DueQlkv = 0,
                        DueFixedMonthlyFee = 0,
                        DueQlts = qlts,
                        DuePrincipal = 0,
                        Installment = total,
                        ClosingPrincipal = principal,
                    });

                    totalInterest += interest;
                    totalQlts += qlts;
                }

                pawnResult.TotalInterest = Math.Round(totalInterest, 0);
                pawnResult.TotalQlkv = 0;
                pawnResult.TotalQlts = Math.Round(totalQlts, 0);
                pawnResult.TotalFixedMonthlyFee = 0;
                pawnResult.InstallmentAmount = pawnResult.Schedule.FirstOrDefault()?.Installment ?? 0;
                pawnResult.TotalPayment = pawnResult.TotalInterest + pawnResult.TotalPeriodicFee;
                return pawnResult;
            }

            // Bảo hiểm cộng vào gốc: tính lãi trên tổng (gốc thực + phí bảo hiểm)
            decimal P = request.PrincipalAmount + request.InsuranceAmountSnapshot;
            int n = request.TermMonths;
            decimal monthlyInterestRate = request.InterestRateMonthly / 100m;
            decimal monthlyQlkvRate     = request.QlkvRateMonthly / 100m;
            decimal monthlyQltsRate     = request.QltsRateMonthly / 100m;
            decimal fixedMonthlyFeeAmount = Math.Max(0, request.FixedMonthlyFeeAmount);
            // Lãi suất tháng + phí định kỳ tháng gộp lại để tính PMT
            decimal rCombined = monthlyInterestRate + monthlyQlkvRate + monthlyQltsRate;

            // PMT = P * r * (1+r)^n / ((1+r)^n – 1)
            decimal power = (decimal)Math.Pow((double)(1 + rCombined), n);
            decimal pmtBase = rCombined == 0 ? P / n
                            : P * rCombined * power / (power - 1);
            decimal pmt = Math.Round(pmtBase + fixedMonthlyFeeAmount, 0);

            // Bước 1: Tính ngày từng kỳ theo chuẩn nghiệp vụ + sheet 18:
            // kỳ n = ngày giải ngân + n tháng (không cộng dồn từ kỳ trước để tránh trôi ngày 31/30/29)
            var periodDates = new List<(DateOnly? From, DateOnly? To, int Days)>(n);
            int totalActualDays = 0;
            DateOnly? installmentDisbursementDate = request.DisbursementDate;

            var dueDates = new List<DateOnly?>(n);
            for (int i = 1; i <= n; i++)
            {
                dueDates.Add(installmentDisbursementDate.HasValue
                    ? installmentDisbursementDate.Value.AddMonths(i)
                    : (DateOnly?)null);
            }

            for (int i = 0; i < n; i++)
            {
                DateOnly? fromDate = i == 0 ? installmentDisbursementDate : dueDates[i - 1];
                DateOnly? toDate = dueDates[i];

                int days = (fromDate.HasValue && toDate.HasValue)
                    ? toDate.Value.DayNumber - fromDate.Value.DayNumber
                    : 30;

                periodDates.Add((fromDate, toDate, days));
                totalActualDays += days;
            }

            var result = new LoanCalculationResult { InstallmentAmount = Math.Round(pmt, 0) };
            decimal balance = P;

            decimal dailyInterestRate = totalActualDays == 0 ? 0 : (monthlyInterestRate * n) / totalActualDays;
            decimal dailyQlkvRate     = totalActualDays == 0 ? 0 : (monthlyQlkvRate * n) / totalActualDays;
            decimal dailyQltsRate     = totalActualDays == 0 ? 0 : (monthlyQltsRate * n) / totalActualDays;

            for (int i = 0; i < n; i++)
            {
                var (periodFrom, periodTo, periodDays) = periodDates[i];
                bool isLastPeriod = (i == n - 1);

                decimal interest = Math.Round(balance * dailyInterestRate * periodDays, 0, MidpointRounding.AwayFromZero);
                decimal qlkv     = Math.Round(balance * dailyQlkvRate * periodDays, 0, MidpointRounding.AwayFromZero);
                decimal qlts     = Math.Round(balance * dailyQltsRate * periodDays, 0, MidpointRounding.AwayFromZero);
                decimal fixedFee = Math.Round(fixedMonthlyFeeAmount, 0, MidpointRounding.AwayFromZero);
                decimal totalFee = interest + qlkv + qlts + fixedFee;

                // Gốc kỳ cuối = toàn bộ nợ còn lại; các kỳ khác = PMT – lãi – phí (làm tròn VNĐ)
                decimal principal = isLastPeriod
                    ? balance
                    : Math.Round(pmt - totalFee, 0, MidpointRounding.AwayFromZero);

                if (principal < 0) principal = 0;
                decimal closing = balance - principal;
                if (closing < 0) closing = 0;

                decimal installmentAmt = isLastPeriod ? (principal + totalFee) : Math.Round(pmt, 0, MidpointRounding.AwayFromZero);

                result.Schedule.Add(new LoanSchedulePreviewItem
                {
                    PeriodNo         = i + 1,
                    FromDate         = periodFrom,
                    ToDate           = periodTo,
                    DayCount         = periodDays,
                    OpeningPrincipal = balance,
                    DueInterest      = interest,
                    DueQlkv          = qlkv,
                    DueFixedMonthlyFee = fixedFee,
                    DueQlts          = qlts,
                    DuePrincipal     = principal,
                    // Cột "Tiền TT hàng kỳ" phải bám PMT cố định như Excel, riêng kỳ cuối điều chỉnh số lẻ
                    Installment      = installmentAmt,
                    ClosingPrincipal = closing,
                });

                result.TotalInterest  += interest;
                result.TotalQlkv      += qlkv;
                result.TotalQlts      += qlts;
                result.TotalFixedMonthlyFee += fixedFee;
                result.TotalPrincipal += principal;

                balance = closing;
            }

            result.TotalInterest  = Math.Round(result.TotalInterest, 0);
            result.TotalQlkv      = Math.Round(result.TotalQlkv, 0);
            result.TotalQlts      = Math.Round(result.TotalQlts, 0);
            result.TotalFixedMonthlyFee = Math.Round(result.TotalFixedMonthlyFee, 0);
            result.TotalPrincipal   = Math.Round(result.TotalPrincipal, 0);
            result.TotalPayment     = result.TotalInterest + result.TotalPeriodicFee + result.TotalPrincipal;
            return result;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Cancel – hủy hợp đồng (không cho sửa, tạo mới nếu sai)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<LoanContract> Cancel(Guid loanContractId, string cancellationReason)
        {
            // Delegate vào ChangeStatus để đảm bảo history luôn được ghi
            return await ChangeStatus(loanContractId, LoanContractStatus.Cancelled, cancellationReason);
        }

        // ──────────────────────────────────────────────────────────────────────
        // ChangeStatus – chuyển trạng thái theo ma trận nghiệp vụ
        // ──────────────────────────────────────────────────────────────────────

        public async Task<LoanContract> ChangeStatus(Guid loanContractId, string toStatus, string? reason)
        {
            var obj = await DbContext.LoanContracts.FindAsync(loanContractId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy hợp đồng với Id = {loanContractId}");

            string fromStatus = obj.StatusCode;

            // Hủy hợp đồng là ngoại lệ: cho phép từ mọi trạng thái hiện có.
            // Nếu đã hủy rồi thì trả lại ngay để giữ tính idempotent.
            if (toStatus != LoanContractStatus.Cancelled)
            {
                // ── Ma trận chuyển trạng thái hợp lệ ─────────────────────────────
                // SETTLED / CLOSED / BAD_DEBT_CLOSED / CANCELLED là terminal – không khai báo ở đây
                var allowedTransitions = new Dictionary<string, string[]>
                {
                    [LoanContractStatus.Draft] = new[] { LoanContractStatus.PendingApproval, LoanContractStatus.Cancelled },
                    [LoanContractStatus.PendingApproval] = new[] { LoanContractStatus.PendingDisbursement, LoanContractStatus.Draft, LoanContractStatus.Cancelled },
                    [LoanContractStatus.PendingDisbursement] = new[] { LoanContractStatus.Disbursed, LoanContractStatus.Cancelled },
                    // DISBURSED: SETTLED xử lý qua LoanSettlementService, nhưng cho phép trigger qua ChangeStatus để đồng bộ
                    [LoanContractStatus.Disbursed] = new[] { LoanContractStatus.PendingBadDebt, LoanContractStatus.BadDebt, LoanContractStatus.Closed, LoanContractStatus.Settled },
                    // PENDING_BAD_DEBT: được duyệt (BAD_DEBT) hoặc từ chối (DISBURSED)
                    [LoanContractStatus.PendingBadDebt] = new[] { LoanContractStatus.BadDebt, LoanContractStatus.Disbursed },
                    [LoanContractStatus.BadDebt] = new[] { LoanContractStatus.BadDebtClosed },
                };

                if (!allowedTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains(toStatus))
                    throw new InvalidOperationException(
                        $"Không thể chuyển hợp đồng từ trạng thái '{fromStatus}' sang '{toStatus}'.");
            }

            if (fromStatus == LoanContractStatus.Cancelled && toStatus == LoanContractStatus.Cancelled)
                return obj;

            // ── Kiểm tra phân quyền cho từng loại transition ──────────────────

            // Hủy hợp đồng: kiểm tra quyền LOAN_CANCEL từ hệ thống phân quyền
            if (toStatus == LoanContractStatus.Cancelled)
            {
                if (!await PermissionChecker.HasPermissionAsync(DbContext, CachingHelper, User.UserId, User.RoleCode, "LOAN_CANCEL"))
                    throw new UnauthorizedAccessException(
                        "Bạn không có quyền hủy hợp đồng. Vui lòng liên hệ quản trị viên.");
            }

            // Duyệt / từ chối: PENDING_APPROVAL → (PENDING_DISBURSEMENT | DRAFT)
            if (fromStatus == LoanContractStatus.PendingApproval &&
                (toStatus == LoanContractStatus.PendingDisbursement || toStatus == LoanContractStatus.Draft))
            {
            if (!User.IsStoreManager && !User.IsRegionalManager && !User.IsAdmin &&
                !await PermissionChecker.HasPermissionAsync(DbContext, CachingHelper, User.UserId, User.RoleCode, "LOAN_APPROVE"))
                    throw new UnauthorizedAccessException(
                        "Chỉ quản lý cửa hàng hoặc admin mới có quyền duyệt/từ chối hợp đồng.");
            }

            // Giải ngân: PENDING_DISBURSEMENT → DISBURSED
            if (fromStatus == LoanContractStatus.PendingDisbursement && toStatus == LoanContractStatus.Disbursed)
            {
            if (!User.IsStoreManager && !User.IsRegionalManager && !User.IsAdmin &&
                !await PermissionChecker.HasPermissionAsync(DbContext, CachingHelper, User.UserId, User.RoleCode, "LOAN_DISBURSE"))
                    throw new UnauthorizedAccessException(
                        "Chỉ quản lý cửa hàng hoặc admin mới có quyền thực hiện giải ngân.");
            }

            // Đóng trực tiếp từ DISBURSED: chỉ Admin (bỏ qua luồng tất toán thông thường)
            if (fromStatus == LoanContractStatus.Disbursed && toStatus == LoanContractStatus.Closed)
            {
                if (!User.IsAdmin)
                    throw new UnauthorizedAccessException(
                        "Chỉ admin mới có thể đóng hợp đồng đang giải ngân mà không qua tất toán.");
            }

            // Chuyển nợ xấu trực tiếp từ DISBURSED: chỉ Manager/Admin hoặc có quyền BAD_DEBT_TRANSFER
            // NHÂN VIÊN phải dùng PENDING_BAD_DEBT để đề xuất, chờ quản lý duyệt
            if (fromStatus == LoanContractStatus.Disbursed && toStatus == LoanContractStatus.BadDebt)
            {
                if (!User.IsStoreManager && !User.IsRegionalManager && !User.IsAdmin &&
                    !await PermissionChecker.HasPermissionAsync(DbContext, CachingHelper, User.UserId, User.RoleCode, "BAD_DEBT_TRANSFER"))
                    throw new UnauthorizedAccessException(
                        "Nhân viên không thể chuyển nợ xấu trực tiếp. Vui lòng dùng chức năng 'Đề xuất nợ xấu'.");
            }

            // Duyệt / từ chối đề xuất nợ xấu: PENDING_BAD_DEBT → (BAD_DEBT | DISBURSED)
            if (fromStatus == LoanContractStatus.PendingBadDebt)
            {
                if (!User.IsStoreManager && !User.IsRegionalManager && !User.IsAdmin &&
                    !await PermissionChecker.HasPermissionAsync(DbContext, CachingHelper, User.UserId, User.RoleCode, "BAD_DEBT_TRANSFER"))
                    throw new UnauthorizedAccessException(
                        "Chỉ quản lý cửa hàng hoặc admin mới có quyền duyệt/từ chối đề xuất nợ xấu.");
            }

            // Đóng hồ sơ nợ xấu (thu hồi xong): StoreManager trở lên
            if (fromStatus == LoanContractStatus.BadDebt && toStatus == LoanContractStatus.BadDebtClosed)
            {
                if (!User.IsStoreManager && !User.IsRegionalManager && !User.IsAdmin)
                    throw new UnauthorizedAccessException(
                        "Chỉ quản lý cửa hàng hoặc admin mới có thể đóng hồ sơ nợ xấu.");
            }

            DateTime now = DateTime.Now;
            Guid? userId = CommonLib.GetGUID(User.UserId);

            // ── Cập nhật timestamp / audit field tương ứng với transition ─────
            switch (toStatus)
            {
                case LoanContractStatus.PendingDisbursement when fromStatus == LoanContractStatus.PendingApproval:
                    var approvedDate = DateOnly.FromDateTime(now);
                    obj.ApprovedDate = approvedDate < obj.ApplicationDate ? obj.ApplicationDate : approvedDate;
                    obj.ApprovedBy = userId;
                    break;

                case LoanContractStatus.Disbursed:
                    obj.DisbursementDate = obj.DisbursementDate ?? DateOnly.FromDateTime(now);
                    if (string.Equals(obj.ContractType, LoanContractType.Pawn, StringComparison.OrdinalIgnoreCase))
                    {
                        var days = obj.PawnPeriodDaysSnapshot <= 0 ? 10 : obj.PawnPeriodDaysSnapshot;
                        obj.MaturityDate = obj.MaturityDate ?? obj.DisbursementDate!.Value.AddDays((int)days * obj.TermMonths);
                    }
                    else
                    {
                        obj.MaturityDate = obj.MaturityDate ?? obj.DisbursementDate!.Value.AddMonths(obj.TermMonths);
                    }
                    obj.DisbursedBy = userId;

                    // Bắt buộc có phiếu chi giải ngân: nếu chưa có thì tự tạo.
                    await EnsureDisbursementVoucherAsync(obj, now, userId);

                    // Tạo lịch trả nợ nếu chưa có
                    bool hasSchedule = await DbContext.LoanRepaymentSchedules
                        .AnyAsync(s => s.LoanContractId == loanContractId);
                    if (!hasSchedule)
                    {
                        var scheduleDisbursementDate = obj.DisbursementDate!.Value;
                        var calcReq = new LoanCalculateRequest
                        {
                            PrincipalAmount        = obj.PrincipalAmount,
                            InsuranceAmountSnapshot = obj.InsuranceAmountSnapshot,
                            ContractType           = obj.ContractType,
                            TermMonths             = obj.TermMonths,
                            InterestRateMonthly    = obj.InterestRateMonthlySnapshot,
                            QlkvRateMonthly        = obj.QlkvRateMonthlySnapshot,
                            QltsRateMonthly        = obj.QltsRateMonthlySnapshot,
                            FixedMonthlyFeeAmount  = obj.FixedMonthlyFeeAmountSnapshot,
                            PawnInterestAmountPerMillionPerDay = obj.PawnInterestAmountPerMillionPerDaySnapshot,
                            PawnFeeAmountPerMillionPerDay      = obj.PawnFeeAmountPerMillionPerDaySnapshot,
                            PawnPeriodDays      = obj.PawnPeriodDaysSnapshot,
                            DisbursementDate       = scheduleDisbursementDate,
                        };
                        var calc = Calculate(calcReq);
                        foreach (var row in calc.Schedule)
                        {
                            DbContext.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
                            {
                                ScheduleId             = Guid.CreateVersion7(),
                                LoanContractId         = loanContractId,
                                PeriodNo               = row.PeriodNo,
                                PeriodFromDate         = row.FromDate!.Value,
                                PeriodToDate           = row.ToDate!.Value,
                                DueDate                = row.ToDate!.Value,
                                ActualDayCount         = row.DayCount,
                                OpeningPrincipalAmount = row.OpeningPrincipal,
                                InstallmentAmount      = row.Installment,
                                DuePrincipalAmount     = row.DuePrincipal,
                                DueInterestAmount      = row.DueInterest,
                                DueQlkvAmount          = row.DueQlkv,
                                DueQltsAmount          = row.DueQlts,
                                DuePeriodicFeeAmount   = row.DuePeriodicFee,
                                DueLatePenaltyAmount   = 0,
                                PaidPrincipalAmount    = 0,
                                PaidInterestAmount     = 0,
                                PaidQlkvAmount         = 0,
                                PaidQltsAmount         = 0,
                                PaidPeriodicFeeAmount  = 0,
                                PaidLatePenaltyAmount  = 0,
                                ClosingPrincipalAmount = row.ClosingPrincipal,
                                StatusCode             = "PENDING",
                                CreatedAt              = now,
                                UpdatedAt              = now,
                            });
                        }
                    }
                    break;

                case LoanContractStatus.BadDebt:
                    // Khi chuyển nợ xấu từ màn hợp đồng: tự tạo hồ sơ nợ xấu để màn danh sách hiển thị được.
                    await EnsureBadDebtCaseAsync(obj, now, userId, reason);
                    break;

                case LoanContractStatus.Settled:
                    obj.SettledAt = now;
                    // Đánh dấu tất cả kỳ chưa thanh toán thành PAID
                    var pendingSchedules = await DbContext.LoanRepaymentSchedules
                        .Where(s => s.LoanContractId == loanContractId && s.StatusCode != ScheduleStatus.Paid)
                        .ToListAsync();

                    foreach (var s in pendingSchedules)
                    {
                        s.StatusCode = ScheduleStatus.Paid;
                        s.FullyPaidAt = now;
                        s.UpdatedAt = now;
                    }
                    break;

                case LoanContractStatus.PendingBadDebt:
                    // Nhân viên đề xuất chuyển nợ xấu: chỉ ghi nhận, chưa tạo hồ sơ nợ xấu
                    break;

                case LoanContractStatus.Cancelled:
                    obj.CancelledAt = now;
                    obj.CancelledBy = userId;
                    obj.CancellationReason = reason;
                    break;

                case LoanContractStatus.Closed:
                case LoanContractStatus.BadDebtClosed:
                    obj.SettledAt = now;
                    break;
            }

            obj.StatusCode = toStatus;

            // ── Ghi lịch sử trạng thái ────────────────────────────────────────
            DbContext.LoanStatusHistories.Add(new LoanStatusHistory
            {
                LoanStatusHistoryId = Guid.CreateVersion7(),
                LoanContractId = loanContractId,
                FromStatusCode = fromStatus,
                ToStatusCode = toStatus,
                ChangedAt = now,
                ChangedBy = userId,
                Reason = reason,
            });

            await DbContext.SaveChangesAsync();
            return obj;
        }

        /// <summary>
        /// Đảm bảo hợp đồng đã giải ngân luôn có phiếu chi giải ngân.
        /// Nếu chưa có thì tự động tạo mới.
        /// </summary>
        private async Task EnsureDisbursementVoucherAsync(LoanContract contract, DateTime now, Guid? userId)
        {
            bool hasDisbursementVoucher = await DbContext.CashVouchers
                .AnyAsync(v => v.LoanContractId == contract.LoanContractId
                            && v.VoucherType == VoucherType.Payment
                            && v.ReasonCode == "LOAN_DISBURSEMENT");

            if (hasDisbursementVoucher)
                return;

            var customerName = await DbContext.Customers
                .Where(c => c.CustomerId == contract.CustomerId)
                .Select(c => c.FullName)
                .FirstOrDefaultAsync();

            decimal disbursementAmount = contract.NetDisbursedAmount > 0
                ? contract.NetDisbursedAmount
                : contract.PrincipalAmount;

            DbContext.CashVouchers.Add(new CashVoucher
            {
                VoucherId = Guid.CreateVersion7(),
                VoucherNo = $"PC-GN-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                StoreId = contract.StoreId,
                VoucherType = VoucherType.Payment,
                ReasonCode = "LOAN_DISBURSEMENT",
                BusinessDate = contract.DisbursementDate ?? DateOnly.FromDateTime(now),
                VoucherDatetime = now,
                CustomerId = contract.CustomerId,
                LoanContractId = contract.LoanContractId,
                PayerReceiverName = customerName,
                Amount = disbursementAmount,
                Description = $"Giải ngân hợp đồng {contract.ContractNo}",
                IsAdjustment = false,
                CreatedBy = userId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        /// <summary>
        /// Đảm bảo khi hợp đồng chuyển BAD_DEBT thì có hồ sơ BadDebtCase tương ứng.
        /// Đồng thời đánh dấu các kỳ chưa trả thành BAD_DEBT.
        /// </summary>
        private async Task EnsureBadDebtCaseAsync(LoanContract contract, DateTime now, Guid? userId, string? note)
        {
            bool existed = await DbContext.BadDebtCases
                .AnyAsync(b => b.LoanContractId == contract.LoanContractId);

            if (!existed)
            {
                var unpaidSchedules = await DbContext.LoanRepaymentSchedules
                    .Where(s => s.LoanContractId == contract.LoanContractId && s.StatusCode != ScheduleStatus.Paid)
                    .ToListAsync();

                decimal outstandingPrincipal = unpaidSchedules.Sum(s => Math.Max(0, s.DuePrincipalAmount - s.PaidPrincipalAmount));
                decimal outstandingInterest = unpaidSchedules.Sum(s => Math.Max(0, s.DueInterestAmount - s.PaidInterestAmount));
                decimal outstandingPeriodicFee = unpaidSchedules.Sum(s => Math.Max(0, s.DuePeriodicFeeAmount - s.PaidPeriodicFeeAmount));
                decimal outstandingLatePenalty = unpaidSchedules.Sum(s => Math.Max(0, s.DueLatePenaltyAmount - s.PaidLatePenaltyAmount));

                DbContext.BadDebtCases.Add(new BadDebtCase
                {
                    BadDebtCaseId = Guid.CreateVersion7(),
                    LoanContractId = contract.LoanContractId,
                    StoreId = contract.StoreId,
                    TransferredAt = now,
                    TransferredBy = userId,
                    OutstandingPrincipalAmount = outstandingPrincipal,
                    OutstandingInterestAmount = outstandingInterest,
                    OutstandingPeriodicFeeAmount = outstandingPeriodicFee,
                    OutstandingLatePenaltyAmount = outstandingLatePenalty,
                    OtherOutstandingAmount = 0,
                    TotalOutstandingAmount = outstandingPrincipal + outstandingInterest + outstandingPeriodicFee + outstandingLatePenalty,
                    RecoveredAmountTotal = 0,
                    StatusCode = BadDebtCaseStatus.Open,
                    Note = note,
                    CreatedAt = now,
                    UpdatedAt = now,
                });

                foreach (var schedule in unpaidSchedules)
                    schedule.StatusCode = ScheduleStatus.BadDebt;

                // Đánh dấu lịch sử nợ xấu vịnh viễn vào hồ sơ khách hàng.
                // Đóng hợp đồng (BAD_DEBT_CLOSED) không xóa flag này.
                var customer = await DbContext.Customers.FindAsync(contract.CustomerId);
                if (customer != null && !customer.HasBadHistory)
                {
                    customer.HasBadHistory = true;
                    if (string.IsNullOrWhiteSpace(customer.BadHistoryNote))
                        customer.BadHistoryNote = $"Nợ xấu hợp đồng {contract.ContractNo} (chuyển {now:dd/MM/yyyy})";
                    customer.UpdatedAt = now;
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // GetStatusHistory – lịch sử thay đổi trạng thái
        // ──────────────────────────────────────────────────────────────────────

        public async Task<IList<LoanStatusHistory>> GetStatusHistory(Guid loanContractId)
        {
            return await DbContext.LoanStatusHistories
                .Where(h => h.LoanContractId == loanContractId)
                .OrderBy(h => h.ChangedAt)
                .ToListAsync();
        }

        public async Task<IList<AuditLogDto>> GetAuditLogs(Guid loanContractId)
        {
            return await DbContext.ContractAuditLogs
                .Include(a => a.ChangedByNavigation)
                .Where(a => a.LoanContractId == loanContractId)
                .OrderByDescending(a => a.ChangedAt)
                .Select(a => new AuditLogDto
                {
                    AuditLogId = a.AuditLogId,
                    TableName = "loan_contracts",
                    ActionCode = a.ActionCode,
                    OldData = a.OldData,
                    NewData = a.NewData,
                    ChangedAt = a.ChangedAt,
                    ChangedBy = a.ChangedBy,
                    ChangedByName = a.ChangedByNavigation != null ? (a.ChangedByNavigation.FullName ?? a.ChangedByNavigation.Username) : null
                })
                .ToListAsync();
        }

        public async Task<int> RecalculateAllPendingSchedulesAsync()
        {
            // TÃ¬m táº¥t cáº£ cÃ¡c há»£p Ä‘á»“ng tráº£ gÃ³p cÃ³ lá»‹ch tráº£ ná»£, nhÆ°ng CHÆ¯A CÃ“ báº¥t ká»³ ká»³ nÃ o Ä‘Ã£ thanh toÃ¡n (PAID hoáº·c PaidPrincipal > 0)
            var eligibleContracts = await DbContext.LoanContracts
                .Include(c => c.LoanRepaymentSchedules)
                .Where(c => c.ContractType == LoanContractType.Installment
                         && c.LoanRepaymentSchedules.Any()
                         && !c.LoanRepaymentSchedules.Any(s => s.StatusCode == ScheduleStatus.Paid || s.PaidPrincipalAmount > 0))
                .ToListAsync();

            int updatedCount = 0;
            foreach (var contract in eligibleContracts)
            {
                var request = new LoanCalculateRequest
                {
                    PrincipalAmount = contract.PrincipalAmount,
                    TermMonths = contract.TermMonths,
                    InterestRateMonthly = contract.InterestRateMonthlySnapshot,
                    QlkvRateMonthly = contract.QlkvRateMonthlySnapshot,
                    QltsRateMonthly = contract.QltsRateMonthlySnapshot,
                    FixedMonthlyFeeAmount = contract.FixedMonthlyFeeAmountSnapshot,
                    DisbursementDate = contract.DisbursementDate,
                    InsuranceAmountSnapshot = contract.InsuranceAmountSnapshot
                };

                var calc = Calculate(request);

                // XÃ³a lá»‹ch cÅ© vÃ  add lá»‹ch má»›i
                DbContext.LoanRepaymentSchedules.RemoveRange(contract.LoanRepaymentSchedules);

                foreach (var row in calc.Schedule)
                {
                    DbContext.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
                    {
                        ScheduleId             = Guid.CreateVersion7(),
                        LoanContractId         = contract.LoanContractId,
                        PeriodNo               = row.PeriodNo,
                        PeriodFromDate         = row.FromDate ?? DateOnly.MinValue,
                        PeriodToDate           = row.ToDate ?? DateOnly.MinValue,
                        DueDate                = row.ToDate ?? DateOnly.MinValue, // Mặc định due date = to date
                        ActualDayCount         = row.DayCount,
                        OpeningPrincipalAmount = row.OpeningPrincipal,
                        InstallmentAmount      = row.Installment,
                        DuePrincipalAmount     = row.DuePrincipal,
                        DueInterestAmount      = row.DueInterest,
                        DueQlkvAmount          = row.DueQlkv,
                        DueQltsAmount          = row.DueQlts,
                        DuePeriodicFeeAmount   = row.DueQlkv + row.DueQlts + row.DueFixedMonthlyFee,
                        ClosingPrincipalAmount = row.ClosingPrincipal,
                        StatusCode             = ScheduleStatus.Pending
                    });
                }
                updatedCount++;
            }

            if (updatedCount > 0)
            {
                await DbContext.SaveChangesAsync();
            }

            return updatedCount;
        }

        // ──────────────────────────────────────────────────────────────────────
        // GetRepaymentSchedule – lịch trả nợ của hợp đồng đã tạo
        // ──────────────────────────────────────────────────────────────────────

        public async Task<IList<LoanRepaymentSchedule>> GetRepaymentSchedule(Guid loanContractId)
        {
            // Lấy hợp đồng để lấy tỷ lệ phạt chậm nộp và ngày bắt đầu tính phạt từ snapshot
            var contract = await DbContext.LoanContracts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.LoanContractId == loanContractId);

            var schedules = await DbContext.LoanRepaymentSchedules
                .Where(s => s.LoanContractId == loanContractId)
                .OrderBy(s => s.PeriodNo)
                .ToListAsync();

            if (contract == null) return schedules;

            // Tính DueLatePenaltyAmount động dựa trên số ngày thực tế quá hạn hôm nay.
            // Công thức nghiệp vụ: phạt = (gốc + lãi + phí kỳ) * tỷ lệ phạt / 100
            // Chỉ áp dụng từ ngày thứ LatePaymentStartDaySnapshot (mặc định 3) trở đi.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var penaltyRate = contract.LatePaymentPenaltyRateSnapshot > 0
                ? contract.LatePaymentPenaltyRateSnapshot
                : 8m; // fallback 8% theo quy định
            var startDay = contract.LatePaymentStartDaySnapshot > 0
                ? contract.LatePaymentStartDaySnapshot
                : (short)3; // phạt từ ngày thứ 4 (dueDate + 3 ngày)

            foreach (var row in schedules)
            {
                // Chỉ tính phạt cho kỳ chưa thanh toán đầy đủ và đã qua hạn
                if (row.StatusCode == "PAID") continue;

                var dueDate = row.DueDate; // already DateOnly
                var daysOverdue = today.DayNumber - dueDate.DayNumber;

                if (daysOverdue < startDay) continue; // chưa đến ngày bắt đầu tính phạt

                // Cơ sở tính phạt: gốc + lãi + phí kỳ còn lại chưa trả
                var basePrincipal = Math.Max(0, row.DuePrincipalAmount   - row.PaidPrincipalAmount);
                var baseInterest  = Math.Max(0, row.DueInterestAmount    - row.PaidInterestAmount);
                var baseFee       = Math.Max(0, row.DuePeriodicFeeAmount - row.PaidPeriodicFeeAmount);
                var penaltyBase   = basePrincipal + baseInterest + baseFee;

                if (penaltyBase <= 0) continue;

                var calculatedPenalty = Math.Round(penaltyBase * penaltyRate / 100, 0, MidpointRounding.AwayFromZero);

                // Ghi đè DueLatePenaltyAmount tính động (KHÔNG lưu DB — chỉ cho response)
                // Giữ nguyên nếu đã có giá trị lớn hơn được persist từ trước
                if (calculatedPenalty > row.DueLatePenaltyAmount)
                    row.DueLatePenaltyAmount = calculatedPenalty;
            }

            return schedules;
        }

        // ──────────────────────────────────────────────────────────────────────
        // SyncScheduleForShortPaymentAsync – tính lại PMT nếu đóng thiếu gốc
        // ──────────────────────────────────────────────────────────────────────
        public async Task<bool> SyncScheduleForShortPaymentAsync(Guid loanContractId)
        {
            var contract = await DbContext.LoanContracts
                .FirstOrDefaultAsync(c => c.LoanContractId == loanContractId);
            if (contract == null) return false;

            if (contract.ContractType == LoanContractType.Pawn) return false; // Cầm đồ không dùng PMT gốc giảm dần
            
            var schedules = await DbContext.LoanRepaymentSchedules
                .Where(s => s.LoanContractId == loanContractId)
                .OrderBy(s => s.PeriodNo)
                .ToListAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);
            
            // Tìm kỳ đã qua hạn gần nhất
            var lastPastDuePeriod = schedules
                .Where(s => s.DueDate <= today)
                .OrderByDescending(s => s.PeriodNo)
                .FirstOrDefault();

            if (lastPastDuePeriod == null) return false;

            // Kiểm tra xem kỳ này có đóng thiếu gốc không
            if (lastPastDuePeriod.PaidPrincipalAmount < lastPastDuePeriod.DuePrincipalAmount)
            {
                var futureSchedules = schedules.Where(s => s.PeriodNo > lastPastDuePeriod.PeriodNo).ToList();
                if (!futureSchedules.Any()) return false;

                // Tính tổng gốc đã trả đến kỳ này
                decimal totalPaidPrincipal = schedules
                    .Where(s => s.PeriodNo <= lastPastDuePeriod.PeriodNo)
                    .Sum(s => s.PaidPrincipalAmount);
                
                decimal remainingPrincipal = (contract.PrincipalAmount + contract.InsuranceAmountSnapshot) - totalPaidPrincipal;

                if (remainingPrincipal <= 0) return false;

                // Lấy snapshot gốc opening của kỳ tiếp theo hiện tại (để xem có trùng khớp không, tránh save dư thừa)
                decimal currentNextOpening = futureSchedules.First().OpeningPrincipalAmount;
                if (Math.Round(currentNextOpening, 0) == Math.Round(remainingPrincipal, 0)) return false; // Đã đồng bộ rồi

                // Bắt đầu tính PMT lại
                int remainingPeriods = futureSchedules.Count;
                decimal monthlyInterestRate = contract.InterestRateMonthlySnapshot / 100m;
                decimal monthlyQlkvRate     = contract.QlkvRateMonthlySnapshot / 100m;
                decimal monthlyQltsRate     = contract.QltsRateMonthlySnapshot / 100m;
                decimal fixedMonthlyFee     = Math.Max(0, contract.FixedMonthlyFeeAmountSnapshot);
                decimal rCombined = monthlyInterestRate + monthlyQlkvRate + monthlyQltsRate;

                decimal power = (decimal)Math.Pow((double)(1 + rCombined), remainingPeriods);
                decimal pmtBase = rCombined == 0 ? remainingPrincipal / remainingPeriods
                                : remainingPrincipal * rCombined * power / (power - 1);
                decimal pmt = Math.Round(pmtBase + fixedMonthlyFee, 0);

                int remainingActualDays = futureSchedules.Sum(s => s.ActualDayCount > 0 ? s.ActualDayCount : 30);
                decimal dailyInterestRate = remainingActualDays == 0 ? 0 : (monthlyInterestRate * remainingPeriods) / remainingActualDays;
                decimal dailyQlkvRate     = remainingActualDays == 0 ? 0 : (monthlyQlkvRate * remainingPeriods) / remainingActualDays;
                decimal dailyQltsRate     = remainingActualDays == 0 ? 0 : (monthlyQltsRate * remainingPeriods) / remainingActualDays;

                decimal balance = remainingPrincipal;

                for (int i = 0; i < remainingPeriods; i++)
                {
                    var row = futureSchedules[i];
                    bool isLastPeriod = (i == remainingPeriods - 1);
                    int periodDays = row.ActualDayCount > 0 ? row.ActualDayCount : 30;

                    decimal interest = Math.Round(balance * dailyInterestRate * periodDays, 0, MidpointRounding.AwayFromZero);
                    decimal qlkv     = Math.Round(balance * dailyQlkvRate * periodDays, 0, MidpointRounding.AwayFromZero);
                    decimal qlts     = Math.Round(balance * dailyQltsRate * periodDays, 0, MidpointRounding.AwayFromZero);
                    decimal fixedFee = Math.Round(fixedMonthlyFee, 0, MidpointRounding.AwayFromZero);
                    decimal totalFee = interest + qlkv + qlts + fixedFee;

                    decimal principal = isLastPeriod ? balance : Math.Round(pmt - totalFee, 0, MidpointRounding.AwayFromZero);
                    if (principal < 0) principal = 0;
                    
                    decimal closing = balance - principal;
                    if (closing < 0) closing = 0;

                    decimal installmentAmt = isLastPeriod ? (principal + totalFee) : Math.Round(pmt, 0, MidpointRounding.AwayFromZero);

                    row.OpeningPrincipalAmount = balance;
                    row.DueInterestAmount = interest;
                    row.DueQlkvAmount = qlkv;
                    row.DueQltsAmount = qlts;
                    row.DuePeriodicFeeAmount = qlkv + qlts + fixedFee;
                    row.DuePrincipalAmount = principal;
                    row.InstallmentAmount = installmentAmt;
                    row.ClosingPrincipalAmount = closing;
                    row.UpdatedAt = DateTime.Now;

                    balance = closing;
                }

                await DbContext.SaveChangesAsync();
                return true;
            }

            return false;
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // GetFinancialSummary – tóm tắt tài chính của hợp đồng
        // ───────────────────────────────────────────────────────────────────────────────
        public async Task<object> GetFinancialSummary(Guid loanContractId)
        {
            var contract = await DbContext.LoanContracts
                .FirstOrDefaultAsync(c => c.LoanContractId == loanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy hợp đồng Id = {loanContractId}");

            var schedules = await DbContext.LoanRepaymentSchedules
                .Where(s => s.LoanContractId == loanContractId)
                .ToListAsync();

            var charges = await DbContext.LoanCharges
                .Where(c => c.LoanContractId == loanContractId)
                .ToListAsync();

            var settlements = await DbContext.LoanSettlements
                .Where(s => s.LoanContractId == loanContractId)
                .ToListAsync();

            var voucherCount = await DbContext.CashVouchers
                .CountAsync(v => v.LoanContractId == loanContractId);

            // Tổng giải ngân = gốc thực + bảo hiểm (đã gộp vào gốc vay)
            decimal totalDisbursed = contract.PrincipalAmount + contract.InsuranceAmountSnapshot;

            // Tổng đã thu theo từng component
            decimal paidPrincipal = schedules.Sum(s => s.PaidPrincipalAmount);
            decimal paidInterest = schedules.Sum(s => s.PaidInterestAmount);
            decimal paidFee = schedules.Sum(s => s.PaidPeriodicFeeAmount);
            decimal paidLatePenalty = schedules.Sum(s => s.PaidLatePenaltyAmount);

            // Phí hồ sơ + bảo hiểm (thu 1 lần)
            // "Cần thu" = snapshot ghi trên hợp đồng (luôn có, ngay khi contract ở DRAFT)
            // "Đã thu"  = PaidAmount tổng hợp từ bảng loan_charges (0 nếu chưa có dòng nào)
            decimal fileFee       = contract.FileFeeAmountSnapshot;
            decimal insurance     = contract.InsuranceAmountSnapshot;
            decimal paidFileFee   = charges.Where(c => c.ChargeCode == "FILE_FEE").Sum(c => c.PaidAmount);
            decimal paidInsurance = charges.Where(c => c.ChargeCode == "INSURANCE").Sum(c => c.PaidAmount);
            decimal remainingFileFee   = Math.Max(0, fileFee - paidFileFee);
            decimal remainingInsurance = Math.Max(0, insurance - paidInsurance);

            // Phạt tất toán sớm (nếu có)
            decimal earlyPenalty = settlements.Sum(s => s.EarlySettlementPenaltyAmount);

            // Dư nợ gốc còn lại
            decimal remainingPrincipal = totalDisbursed - paidPrincipal;

            // Tổng tiền phải trả còn lại (theo lịch) + phí hồ sơ & bảo hiểm chưa thu
            decimal scheduleRemaining = schedules
                .Where(s => s.StatusCode != "PAID")
                .Sum(s => (s.DuePrincipalAmount - s.PaidPrincipalAmount)
                        + (s.DueInterestAmount - s.PaidInterestAmount)
                        + (s.DuePeriodicFeeAmount - s.PaidPeriodicFeeAmount)
                        + (s.DueLatePenaltyAmount - s.PaidLatePenaltyAmount));

            // Tổng tiền phải trả còn lại (theo lịch) + phí hồ sơ chưa thu
            // Bảo hiểm KHÔNG cộng thêm vào đây vì đã gộp vào gốc vay (nằm trong scheduleRemaining rồi)
            decimal totalDueRemaining = scheduleRemaining + remainingFileFee;

            int paidPeriods    = schedules.Count(s => s.StatusCode == "PAID");
            int pendingPeriods = schedules.Count(s => s.StatusCode == "PENDING" || s.StatusCode == "PARTIAL");
            int overduePeriods = schedules.Count(s => s.StatusCode == "OVERDUE");
            // Nếu chưa có lịch (chưa giải ngân), dùng số kỳ từ hợp đồng làm expected
            int totalPeriods   = schedules.Count > 0 ? schedules.Count : contract.TermMonths;

            return new
            {
                LoanContractId = loanContractId,
                ContractCode = contract.ContractNo,
                StatusCode = contract.StatusCode,
                PrincipalAmount = totalDisbursed,
                TotalPeriods = totalPeriods,
                PaidPeriods = paidPeriods,
                PendingPeriods = pendingPeriods,
                OverduePeriods = overduePeriods,
                // Thu gốc
                PaidPrincipal = paidPrincipal,
                RemainingPrincipal = remainingPrincipal,
                // Thu lãi
                PaidInterest = paidInterest,
                // Thu phí định kỳ
                PaidPeriodicFee = paidFee,
                // Thu phạt chậm nộp
                PaidLatePenalty = paidLatePenalty,
                // Phí hồ sơ + bảo hiểm
                FileFee              = fileFee,
                PaidFileFee          = paidFileFee,
                RemainingFileFee     = remainingFileFee,
                Insurance            = insurance,
                PaidInsurance        = paidInsurance,
                RemainingInsurance   = remainingInsurance,
                // Phạt tất toán sớm
                EarlySettlementPenalty = earlyPenalty,
                // Tổng thực thu (chỉ tính các khoản đã thực sự nhận tiền)
                TotalCollected = paidPrincipal + paidInterest + paidFee + paidLatePenalty + paidFileFee + paidInsurance + earlyPenalty,
                // Tổng lãi/phí thực thu ("lợi nhuận" từ hợp đồng)
                TotalIncomeCollected = paidInterest + paidFee + paidLatePenalty + paidFileFee + paidInsurance + earlyPenalty,
                // Còn phải thu
                TotalDueRemaining = totalDueRemaining,
                VoucherCount = voucherCount,
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // GetOverdueList – danh sách kỳ chậm nộp từ DB view
        // ──────────────────────────────────────────────────────────────────────

        public async Task<IList<VOverdueSchedule>> GetOverdueList(int? minDaysOverdue, int? maxDaysOverdue)
        {
            var query = DbContext.VOverdueSchedules.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(v => v.StoreId.HasValue && storeScopeIds.Contains(v.StoreId.Value));

            if (minDaysOverdue.HasValue)
                query = query.Where(v => v.DaysOverdue >= minDaysOverdue.Value);

            if (maxDaysOverdue.HasValue)
                query = query.Where(v => v.DaysOverdue <= maxDaysOverdue.Value);

            return await query.OrderByDescending(v => v.DaysOverdue).ToListAsync();
        }

        // ──────────────────────────────────────────────────────────────────────
        // AssignLoanContract – chuyển giao hợp đồng cho nhân viên khác
        // ──────────────────────────────────────────────────────────────────────

        public async Task<LoanContract> AssignLoanContract(Guid loanContractId, Guid targetUserId)
        {
            if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
                throw new UnauthorizedAccessException(
                    "Chỉ quản lý cửa hàng hoặc admin mới có quyền chuyển giao hợp đồng.");

            var obj = await DbContext.LoanContracts
                      .Include(c => c.Store)
                      .FirstOrDefaultAsync(c => c.LoanContractId == loanContractId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy hợp đồng với Id = {loanContractId}");

            // StoreManager chỉ phân công trong phạm vi cửa hàng mình
            if (!User.IsAdmin)
            {
                var storeScopeIds = GetStoreScopeIds();
                if (storeScopeIds is not null && !storeScopeIds.Any(id => id == obj.StoreId))
                    throw new UnauthorizedAccessException(
                        "Bạn không có quyền chuyển giao hợp đồng của chi nhánh khác.");
            }

            // Lấy thông tin nhân viên mới để xác định chi nhánh đích
            var targetUser = await DbContext.AppUsers
                .Include(u => u.Store)
                .FirstOrDefaultAsync(u => u.UserId == targetUserId)
                ?? throw new KeyNotFoundException($"Không tìm thấy nhân viên với Id = {targetUserId}");

            // Snapshot dữ liệu cũ để ghi log
            var oldAssignedUserId = obj.AssignedToUserId;
            var oldStoreId = obj.StoreId;
            var oldStoreName = obj.Store?.StoreName;

            // Cập nhật người phụ trách + chi nhánh
            obj.AssignedToUserId = targetUserId;
            if (targetUser.StoreId.HasValue)
                obj.StoreId = targetUser.StoreId.Value;
            obj.UpdatedAt = DateTime.Now;

            await DbContext.SaveChangesAsync();
            return obj;
        }
    }
}






