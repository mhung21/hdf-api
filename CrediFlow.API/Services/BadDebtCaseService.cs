using CrediFlow.API.Models;
using CrediFlow.API.Utils;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface IBadDebtCaseService : IBaseService<BadDebtCase>
    {
        /// <summary>Tạo mới hoặc cập nhật hồ sơ nợ xấu.</summary>
        Task<BadDebtCase> Save(CUBadDebtCaseModel model);

        /// <summary>Lấy danh sách hồ sơ nợ xấu theo quyền.</summary>
        Task<IList<BadDebtCase>> GetAlls();

        /// <summary>Tìm kiếm hồ sơ nợ xấu với phân trang và sắp xếp.</summary>
        Task<object> SearchBadDebtCase(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);

        /// <summary>Chuyển hợp đồng vay sang nợ xấu (thủ công bởi quản lý).</summary>
        Task<BadDebtCase> TransferFromLoan(Guid loanContractId, string? note);

        /// <summary>Ghi nhận một khoản thu hồi nợ xấu — tạo phiếu thu BAD_DEBT_RECOVERY và cập nhật hồ sơ.</summary>
        Task<BadDebtCase> RecordRecovery(RecordBadDebtRecoveryModel model);
    }

    public class BadDebtCaseService : BaseService<BadDebtCase, CrediflowContext>, IBadDebtCaseService
    {
        public BadDebtCaseService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<BadDebtCase>> GetAlls()
        {
            var query = DbContext.BadDebtCases.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(b => storeScopeIds.Contains(b.StoreId));

            // Staff chỉ thấy nợ xấu liên quan đến hợp đồng mình phụ trách
            if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
            {
                var staffId = CommonLib.GetGUID(User.UserId);
                query = query.Where(b =>
                    b.LoanContract.CreatedBy == staffId ||
                    b.LoanContract.AssignedToUserId == staffId);
            }

            return await query.OrderByDescending(b => b.TransferredAt).ToListAsync();
        }

        public async Task<BadDebtCase> Save(CUBadDebtCaseModel model)
        {
            bool isCreate = model.BadDebtCaseId == null || model.BadDebtCaseId == Guid.Empty;
            BadDebtCase obj;

            if (isCreate)
            {
                obj = new BadDebtCase
                {
                    BadDebtCaseId = Guid.CreateVersion7(),
                    TransferredAt = DateTime.Now,
                    TransferredBy = CommonLib.GetGUID(User.UserId),
                };
                DbContext.BadDebtCases.Add(obj);
            }
            else
            {
                obj = await DbContext.BadDebtCases.FindAsync(model.BadDebtCaseId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ nợ xấu với Id = {model.BadDebtCaseId}");
            }

            obj.LoanContractId                  = model.LoanContractId;
            obj.StoreId                         = model.StoreId;
            obj.OutstandingPrincipalAmount       = model.OutstandingPrincipalAmount;
            obj.OutstandingInterestAmount        = model.OutstandingInterestAmount;
            obj.OutstandingPeriodicFeeAmount     = model.OutstandingPeriodicFeeAmount;
            obj.OutstandingLatePenaltyAmount     = model.OutstandingLatePenaltyAmount;
            obj.OtherOutstandingAmount           = model.OtherOutstandingAmount;
            obj.TotalOutstandingAmount           = model.TotalOutstandingAmount;
            obj.RecoveredAmountTotal             = model.RecoveredAmountTotal;
            obj.StatusCode                       = model.StatusCode;
            obj.Note                             = model.Note ?? obj.Note;

            // Cập nhật ClosedAt khi đóng hồ sơ
            if (model.StatusCode == BadDebtCaseStatus.Closed && obj.ClosedAt == null)
            {
                obj.ClosedAt  = DateTime.Now;
                obj.ClosedBy  = CommonLib.GetGUID(User.UserId);

                // Kiểm tra và tự động xóa nợ xấu cho khách hàng nếu tất cả hồ sơ đều đã đóng
                var contract = await DbContext.LoanContracts.FindAsync(obj.LoanContractId);
                if (contract != null)
                {
                    bool hasOtherOpenBadDebts = await DbContext.BadDebtCases
                        .AnyAsync(b => b.LoanContract.CustomerId == contract.CustomerId && 
                                       b.BadDebtCaseId != obj.BadDebtCaseId && 
                                       b.StatusCode != BadDebtCaseStatus.Closed);
                    
                    if (!hasOtherOpenBadDebts)
                    {
                        var customer = await DbContext.Customers.FindAsync(contract.CustomerId);
                        if (customer != null && customer.HasBadHistory)
                        {
                            customer.HasBadHistory = false;
                            customer.BadHistoryNote = $"{customer.BadHistoryNote} (đã thanh toán toàn bộ nợ xấu lúc {DateTime.Now:dd/MM/yyyy})".Trim();
                        }
                    }
                }
            }

            await DbContext.SaveChangesAsync();
            return obj;
        }

        public async Task<object> SearchBadDebtCase(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;

            var query = DbContext.BadDebtCases
                .Include(b => b.LoanContract).ThenInclude(c => c.Customer)
                .Include(b => b.Store)
                .Include(b => b.TransferredByNavigation)
                .AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(b => storeScopeIds.Contains(b.StoreId));

            // Staff chỉ thấy nợ xấu liên quan đến hợp đồng mình phụ trách
            if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
            {
                var staffId = CommonLib.GetGUID(User.UserId);
                query = query.Where(b =>
                    b.LoanContract.CreatedBy == staffId ||
                    b.LoanContract.AssignedToUserId == staffId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(b =>
                    b.StatusCode.ToLower().Contains(keyword) ||
                    (b.Note != null && b.Note.ToLower().Contains(keyword)) ||
                    b.LoanContract.Customer.FullName.ToLower().Contains(keyword) ||
                    b.LoanContract.Customer.NationalId.ToLower().Contains(keyword) ||
                    b.LoanContract.ContractNo.ToLower().Contains(keyword));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "transferredat") switch
            {
                "statuscode"            => sortDesc ? query.OrderByDescending(b => b.StatusCode)           : query.OrderBy(b => b.StatusCode),
                "totaloutstandingamount"=> sortDesc ? query.OrderByDescending(b => b.TotalOutstandingAmount): query.OrderBy(b => b.TotalOutstandingAmount),
                "createdat"             => sortDesc ? query.OrderByDescending(b => b.CreatedAt)            : query.OrderBy(b => b.CreatedAt),
                _                       => sortDesc ? query.OrderByDescending(b => b.TransferredAt)        : query.OrderBy(b => b.TransferredAt)
            };

            var items = await sorted
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.BadDebtCaseId,
                    b.LoanContractId,
                    ContractCode        = b.LoanContract.ContractNo,
                    CustomerName        = b.LoanContract.Customer.FullName,
                    CustomerNationalId  = b.LoanContract.Customer.NationalId,
                    b.StoreId,
                    StoreName           = b.Store.StoreName,
                    TransferDate        = b.TransferredAt,
                    TransferredByName   = b.TransferredByNavigation != null ? b.TransferredByNavigation.FullName : null,
                    b.OutstandingPrincipalAmount,
                    b.OutstandingInterestAmount,
                    b.OutstandingPeriodicFeeAmount,
                    b.OutstandingLatePenaltyAmount,
                    b.OtherOutstandingAmount,
                    b.TotalOutstandingAmount,
                    b.RecoveredAmountTotal,
                    b.StatusCode,
                    b.Note,
                    b.CreatedAt,
                })
                .ToListAsync();

            return new { TotalCount = total, PageIndex = pageIndex, PageSize = pageSize, Items = items };
        }

        // ──────────────────────────────────────────────────────────────────────
        // TransferFromLoan – chuyển hợp đồng sang nợ xấu (thủ công)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<BadDebtCase> TransferFromLoan(Guid loanContractId, string? note)
        {
            // Chỉ Manager / Admin hoặc người có quyền BAD_DEBT_TRANSFER được chuyển nợ xấu trực tiếp.
            // Nhân viên phải sử dụng ChangeStatus(PENDING_BAD_DEBT) trên màn hợp đồng và chờ quản lý duyệt.
            if (!User.IsStoreManager && !User.IsRegionalManager && !User.IsAdmin &&
                !await PermissionChecker.HasPermissionAsync(DbContext, CachingHelper, User.UserId, User.RoleCode, "BAD_DEBT_TRANSFER"))
                throw new UnauthorizedAccessException(
                    "Nhân viên không thể chuyển nợ xấu trực tiếp. Vui lòng dùng chức năng 'Đề xuất nợ xấu' và chờ quản lý duyệt.");

            var contract = await DbContext.LoanContracts
                .FirstOrDefaultAsync(c => c.LoanContractId == loanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy hợp đồng Id = {loanContractId}");

            if (contract.StatusCode == LoanContractStatus.Cancelled ||
                contract.StatusCode == LoanContractStatus.Settled   ||
                contract.StatusCode == LoanContractStatus.Closed    ||
                contract.StatusCode == LoanContractStatus.BadDebt)
                throw new InvalidOperationException($"Hop dong dang o trang thai '{contract.StatusCode}', khong the chuyen no xau.");

            // Query rieng cho cac ky chua thanh toan, khong dung Include
            var unpaid = await DbContext.LoanRepaymentSchedules
                .Where(s => s.LoanContractId == loanContractId && s.StatusCode != ScheduleStatus.Paid)
                .ToListAsync();

            decimal principal  = unpaid.Sum(s => s.OpeningPrincipalAmount  - s.PaidPrincipalAmount);
            decimal interest   = unpaid.Sum(s => s.DueInterestAmount       - s.PaidInterestAmount);
            decimal fee        = unpaid.Sum(s => s.DuePeriodicFeeAmount    - s.PaidPeriodicFeeAmount);
            decimal latePenalty= unpaid.Sum(s => s.DueLatePenaltyAmount    - s.PaidLatePenaltyAmount);

            using var tx = await DbContext.Database.BeginTransactionAsync();

            // Tạo hồ sơ nợ xấu
            var badDebt = new BadDebtCase
            {
                BadDebtCaseId                  = Guid.CreateVersion7(),
                LoanContractId                 = loanContractId,
                StoreId                        = contract.StoreId,
                TransferredAt                  = DateTime.Now,
                TransferredBy                  = CommonLib.GetGUID(User.UserId),
                OutstandingPrincipalAmount      = principal,
                OutstandingInterestAmount       = interest,
                OutstandingPeriodicFeeAmount    = fee,
                OutstandingLatePenaltyAmount    = latePenalty,
                OtherOutstandingAmount          = 0,
                TotalOutstandingAmount          = principal + interest + fee + latePenalty,
                RecoveredAmountTotal            = 0,
                StatusCode                     = BadDebtCaseStatus.Open,
                Note                           = note,
            };
            DbContext.BadDebtCases.Add(badDebt);

            // Cập nhật trạng thái hợp đồng
            contract.StatusCode = LoanContractStatus.BadDebt;

            // Cập nhật trạng thái các kỳ chưa thanh toán
            foreach (var s in unpaid)
                s.StatusCode = ScheduleStatus.BadDebt;

            await DbContext.SaveChangesAsync();
            await tx.CommitAsync();

            return badDebt;
        }

        // ──────────────────────────────────────────────────────────────────────
        // RecordRecovery – ghi nhận khoản thu hồi nợ xấu
        // ──────────────────────────────────────────────────────────────────────

        public async Task<BadDebtCase> RecordRecovery(RecordBadDebtRecoveryModel model)
        {
            var badDebt = await DbContext.BadDebtCases
                .Include(b => b.LoanContract).ThenInclude(c => c.Customer)
                .FirstOrDefaultAsync(b => b.BadDebtCaseId == model.BadDebtCaseId)
                ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ nợ xấu Id = {model.BadDebtCaseId}");

            if (badDebt.StatusCode == BadDebtCaseStatus.Closed)
                throw new InvalidOperationException("Hồ sơ nợ xấu đã đóng, không thể ghi nhận thêm khoản thu hồi.");

            if (model.Amount <= 0)
                throw new InvalidOperationException("Số tiền thu hồi phải lớn hơn 0.");

            using var tx = await DbContext.Database.BeginTransactionAsync();

            // Tạo phiếu thu BAD_DEBT_RECOVERY
            var voucher = new CashVoucher
            {
                VoucherId            = Guid.CreateVersion7(),
                VoucherNo            = $"THNO-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                StoreId              = badDebt.StoreId,
                VoucherType          = VoucherType.Receipt,
                ReasonCode           = VoucherReasonCode.BadDebtRecovery,
                BusinessDate         = model.BusinessDate,
                VoucherDatetime      = DateTime.Now,
                CustomerId           = badDebt.LoanContract.CustomerId,
                LoanContractId       = badDebt.LoanContractId,
                PayerReceiverName    = badDebt.LoanContract.Customer?.FullName,
                Amount               = model.Amount,
                Description          = model.Note ?? $"Thu hồi nợ xấu hợp đồng {badDebt.LoanContract.ContractNo}",
                IsAdjustment         = false,
                CreatedBy            = CommonLib.GetGUID(User.UserId),
            };
            DbContext.CashVouchers.Add(voucher);

            // Cập nhật hồ sơ nợ xấu
            badDebt.RecoveredAmountTotal += model.Amount;

            bool fullyRecovered = badDebt.RecoveredAmountTotal >= badDebt.TotalOutstandingAmount;

            if (fullyRecovered)
            {
                badDebt.StatusCode = BadDebtCaseStatus.Closed;
                badDebt.ClosedAt   = DateTime.Now;
                badDebt.ClosedBy   = CommonLib.GetGUID(User.UserId);

                // Cập nhật trạng thái hợp đồng → BAD_DEBT_CLOSED
                badDebt.LoanContract.StatusCode = LoanContractStatus.BadDebtClosed;

                // Kiểm tra xem khách hàng có còn hồ sơ nợ xấu nào đang mở không
                var customerId = badDebt.LoanContract.CustomerId;
                bool hasOtherOpen = await DbContext.BadDebtCases
                    .AnyAsync(b => b.LoanContract.CustomerId == customerId
                                && b.BadDebtCaseId != badDebt.BadDebtCaseId
                                && b.StatusCode     != BadDebtCaseStatus.Closed);

                if (!hasOtherOpen && badDebt.LoanContract.Customer != null && badDebt.LoanContract.Customer.HasBadHistory)
                {
                    badDebt.LoanContract.Customer.HasBadHistory    = false;
                    badDebt.LoanContract.Customer.BadHistoryNote   =
                        $"{badDebt.LoanContract.Customer.BadHistoryNote} (đã thanh toán toàn bộ nợ xấu lúc {DateTime.Now:dd/MM/yyyy})".Trim();
                }
            }
            else
            {
                badDebt.StatusCode = BadDebtCaseStatus.Recovering;
            }

            await DbContext.SaveChangesAsync();
            await tx.CommitAsync();

            return badDebt;
        }
    }
}
