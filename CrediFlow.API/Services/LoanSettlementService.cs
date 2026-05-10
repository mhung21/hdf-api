using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ILoanSettlementService : IBaseService<LoanSettlement>
    {
        /// <summary>Tạo mới hoặc cập nhật phiếu tất toán.</summary>
        Task<LoanSettlement> Save(CULoanSettlementModel model);

        /// <summary>Lấy danh sách tất toán theo quyền.</summary>
        Task<IList<LoanSettlement>> GetAlls();

        /// <summary>Tìm kiếm tất toán với phân trang và sắp xếp.</summary>
        Task<object> SearchLoanSettlement(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);

        /// <summary>
        /// Tính số tiền tất toán tại ngày cụ thể:
        /// kiểm tra tỷ lệ 80% thời gian vay, tính phần trăm phạt tất toán sớm nếu cần.
        /// </summary>
        Task<SettlementCalculationResult> Calculate(Guid loanContractId, DateOnly settlementDate);
    }

    public class SettlementCalculationResult
    {
        public Guid    LoanContractId            { get; set; }
        public string  ContractNo                { get; set; } = string.Empty;
        public DateOnly SettlementDate           { get; set; }
        public int     ContractTotalDays         { get; set; }
        public int     ActualElapsedDays         { get; set; }
        public decimal CompletionRatio           { get; set; }   // phần trăm % thời gian đã trải qua
        public bool    IsEarlySettlement         { get; set; }   // true nếu < 80%
        public decimal RemainingPrincipalAmount  { get; set; }
        public decimal AccruedInterestAmount     { get; set; }
        public decimal AccruedPeriodicFeeAmount  { get; set; }
        public decimal UnpaidLatePenaltyAmount   { get; set; }
        public decimal EarlySettlementPenaltyAmount { get; set; }
        public decimal TotalSettlementAmount     { get; set; }
        public string  SettlementType            { get; set; } = string.Empty;
    }

    public class LoanSettlementService : BaseService<LoanSettlement, CrediflowContext>, ILoanSettlementService
    {
        public LoanSettlementService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<LoanSettlement>> GetAlls()
        {
            var query = DbContext.LoanSettlements.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(s => storeScopeIds.Contains(s.LoanContract.StoreId));

            return await query.OrderByDescending(s => s.RequestDate).ToListAsync();
        }

        public async Task<LoanSettlement> Save(CULoanSettlementModel model)
        {
            bool isCreate = model.SettlementId == null || model.SettlementId == Guid.Empty;
            LoanSettlement obj;

            if (isCreate)
            {
                obj = new LoanSettlement
                {
                    SettlementId  = Guid.CreateVersion7(),
                    SettlementNo  = $"TT{DateTime.Now:yyyyMMddHHmmss}",
                    IsFinalized   = false,
                    CreatedBy     = CommonLib.GetGUID(User.UserId),
                };
                DbContext.LoanSettlements.Add(obj);
            }
            else
            {
                obj = await DbContext.LoanSettlements.FindAsync(model.SettlementId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy phiếu tất toán với Id = {model.SettlementId}");
            }

            obj.LoanContractId               = model.LoanContractId;
            obj.SettlementType               = model.SettlementType;
            obj.RequestDate                  = model.RequestDate;
            obj.SettlementDate               = model.SettlementDate               ?? obj.SettlementDate;
            obj.ActualElapsedDays            = model.ActualElapsedDays;
            obj.ContractTotalDays            = model.ContractTotalDays;
            obj.CompletionRatio              = model.CompletionRatio;
            obj.RemainingPrincipalAmount     = model.RemainingPrincipalAmount;
            obj.AccruedInterestAmount        = model.AccruedInterestAmount;
            obj.AccruedPeriodicFeeAmount     = model.AccruedPeriodicFeeAmount;
            obj.UnpaidLatePenaltyAmount      = model.UnpaidLatePenaltyAmount;
            obj.EarlySettlementPenaltyAmount = model.EarlySettlementPenaltyAmount;
            obj.OtherReceivableAmount        = model.OtherReceivableAmount;
            obj.DiscountAmount               = model.DiscountAmount;
            obj.TotalSettlementAmount        = model.TotalSettlementAmount;
            obj.Note                         = model.Note ?? obj.Note;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        public async Task<object> SearchLoanSettlement(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;


            var query = DbContext.LoanSettlements.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(s => storeScopeIds.Contains(s.LoanContract.StoreId));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(s =>
                    s.SettlementNo.ToLower().Contains(keyword) ||
                    s.SettlementType.ToLower().Contains(keyword) ||
                    (s.Note != null && s.Note.ToLower().Contains(keyword)));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "requestdate") switch
            {
                "settlementno"           => sortDesc ? query.OrderByDescending(s => s.SettlementNo)           : query.OrderBy(s => s.SettlementNo),
                "settlementtype"         => sortDesc ? query.OrderByDescending(s => s.SettlementType)         : query.OrderBy(s => s.SettlementType),
                "totalsettlementamount"  => sortDesc ? query.OrderByDescending(s => s.TotalSettlementAmount)  : query.OrderBy(s => s.TotalSettlementAmount),
                "createdat"              => sortDesc ? query.OrderByDescending(s => s.CreatedAt)              : query.OrderBy(s => s.CreatedAt),
                _                        => sortDesc ? query.OrderByDescending(s => s.RequestDate)            : query.OrderBy(s => s.RequestDate)
            };

            var items = await sorted.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new { Total = total, PageIndex = pageIndex, PageSize = pageSize, Items = items };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Calculate – tính tất toán tại ngày cụ thể
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SettlementCalculationResult> Calculate(Guid loanContractId, DateOnly settlementDate)
        {
            var contract = await DbContext.LoanContracts
                .FirstOrDefaultAsync(c => c.LoanContractId == loanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy hợp đồng với Id = {loanContractId}");

            // Tính tổng ngày hợp đống và số ngày đã trải qua
            var disbursementDate = contract.DisbursementDate ?? contract.ApplicationDate;
            var maturityDate     = contract.MaturityDate     ?? disbursementDate.AddMonths(contract.TermMonths);

            int contractTotalDays = maturityDate.DayNumber - disbursementDate.DayNumber;
            int elapsedDays       = settlementDate.DayNumber - disbursementDate.DayNumber;
            if (elapsedDays < 0) elapsedDays = 0;

            decimal completionRatio = contractTotalDays > 0
                ? Math.Round((decimal)elapsedDays / contractTotalDays * 100, 2)
                : 100m;

            bool isEarly = completionRatio < 80m;

            // Gộp số tiền còn nợ từ lịch trả nợ (tất cả các kỳ)
            var schedules = await DbContext.LoanRepaymentSchedules
                .Where(s => s.LoanContractId == loanContractId)
                .OrderBy(s => s.PeriodNo)
                .ToListAsync();

            var isPawn = contract.ContractType == "PAWN";
            
            // Tìm gốc còn lại từ kỳ chưa trả đầu tiên
            var firstUnpaid = schedules.FirstOrDefault(s => s.StatusCode != ScheduleStatus.Paid);
            decimal remainingPrincipal = firstUnpaid?.OpeningPrincipalAmount ?? contract.PrincipalAmount;

            decimal accruedInterest = 0m;
            decimal accruedFee = 0m;
            decimal unpaidLatePenalty = 0m;

            foreach (var row in schedules)
            {
                if (row.DueDate < settlementDate)
                {
                    // Kỳ đã qua hạn hoàn toàn
                    accruedInterest += row.DueInterestAmount - row.PaidInterestAmount;
                    accruedFee += row.DuePeriodicFeeAmount - row.PaidPeriodicFeeAmount;
                    unpaidLatePenalty += row.DueLatePenaltyAmount - row.PaidLatePenaltyAmount;
                }
                else if (row.PeriodFromDate <= settlementDate)
                {
                    // Kỳ hiện tại đang chạy
                    int daysElapsed = Math.Max(1, settlementDate.DayNumber - row.PeriodFromDate.DayNumber);
                    decimal proratedInterest = 0m;
                    decimal proratedFee = 0m;

                    if (isPawn)
                    {
                        decimal dailyInterestRate = contract.PawnInterestAmountPerMillionPerDaySnapshot / 1_000_000m;
                        decimal dailyFeeRate = contract.PawnFeeAmountPerMillionPerDaySnapshot / 1_000_000m;
                        decimal opening = row.OpeningPrincipalAmount > 0 ? row.OpeningPrincipalAmount : remainingPrincipal;

                        proratedInterest = Math.Round(opening * dailyInterestRate * daysElapsed, 0);
                        proratedFee = Math.Round(opening * dailyFeeRate * daysElapsed, 0);
                    }
                    else
                    {
                        int daysInPeriod = Math.Max(1, row.ActualDayCount);
                        decimal ratio = Math.Min(1m, (decimal)daysElapsed / daysInPeriod);
                        proratedInterest = Math.Round(row.DueInterestAmount * ratio, 0);
                        proratedFee = Math.Round(row.DuePeriodicFeeAmount * ratio, 0);
                    }

                    accruedInterest += proratedInterest - row.PaidInterestAmount;
                    accruedFee += proratedFee - row.PaidPeriodicFeeAmount;
                    unpaidLatePenalty += row.DueLatePenaltyAmount - row.PaidLatePenaltyAmount;
                }
                else
                {
                    // Kỳ tương lai: hoàn trả toàn bộ số tiền đã đóng trước
                    accruedInterest -= row.PaidInterestAmount;
                    accruedFee -= row.PaidPeriodicFeeAmount;
                    unpaidLatePenalty -= row.PaidLatePenaltyAmount;
                }
            }

            // Phạt tất toán sớm (có thể cấu hình qua EarlySettlementPenaltyRateSnapshot)
            decimal earlyPenalty = isEarly
                ? Math.Round(remainingPrincipal * contract.EarlySettlementPenaltyRateSnapshot / 100m, 0)
                : 0m;

            decimal total = remainingPrincipal + accruedInterest + accruedFee + unpaidLatePenalty + earlyPenalty;

            return new SettlementCalculationResult
            {
                LoanContractId             = loanContractId,
                ContractNo                 = contract.ContractNo,
                SettlementDate             = settlementDate,
                ContractTotalDays          = contractTotalDays,
                ActualElapsedDays          = elapsedDays,
                CompletionRatio            = completionRatio,
                IsEarlySettlement          = isEarly,
                RemainingPrincipalAmount   = remainingPrincipal,
                AccruedInterestAmount      = accruedInterest,
                AccruedPeriodicFeeAmount   = accruedFee,
                UnpaidLatePenaltyAmount    = unpaidLatePenalty,
                EarlySettlementPenaltyAmount = earlyPenalty,
                TotalSettlementAmount      = total,
                SettlementType             = isEarly ? SettlementType.Early : SettlementType.OnTime,
            };
        }
    }
}
