using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ILoanChargeService : IBaseService<LoanCharge>
    {
        /// <summary>Tạo mới hoặc cập nhật khoản phí hợp đồng.</summary>
        Task<LoanCharge> Save(CULoanChargeModel model);

        /// <summary>Lấy danh sách phí hợp đồng theo quyền.</summary>
        Task<IList<LoanCharge>> GetAlls();

        /// <summary>Tìm kiếm phí hợp đồng với phân trang và sắp xếp.</summary>
        Task<object> SearchLoanCharge(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);
    }

    public class LoanChargeService : BaseService<LoanCharge, CrediflowContext>, ILoanChargeService
    {
        public LoanChargeService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<LoanCharge>> GetAlls()
        {
            var query = DbContext.LoanCharges.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(c => storeScopeIds.Contains(c.LoanContract.StoreId));

            return await query.OrderByDescending(c => c.DueDate).ToListAsync();
        }

        public async Task<LoanCharge> Save(CULoanChargeModel model)
        {
            bool isCreate = model.ChargeId == null || model.ChargeId == Guid.Empty;
            LoanCharge obj;

            if (isCreate)
            {
                obj = new LoanCharge
                {
                    ChargeId          = Guid.CreateVersion7(),
                    IsSystemGenerated = false,
                    StatusCode        = LoanChargeStatus.Open,
                    PaidAmount        = 0,
                    WaivedAmount      = 0,
                    CreatedBy         = CommonLib.GetGUID(User.UserId),
                };
                DbContext.LoanCharges.Add(obj);
            }
            else
            {
                obj = await DbContext.LoanCharges.FindAsync(model.ChargeId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy khoản phí với Id = {model.ChargeId}");
            }

            obj.LoanContractId = model.LoanContractId;
            obj.ScheduleId     = model.ScheduleId     ?? obj.ScheduleId;
            obj.ChargeCode     = model.ChargeCode;
            obj.ChargeName     = model.ChargeName;
            obj.ChargeDate     = model.ChargeDate;
            obj.DueDate        = model.DueDate;
            obj.Amount         = model.Amount;
            obj.Note           = model.Note ?? obj.Note;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        public async Task<object> SearchLoanCharge(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = DbContext.LoanCharges.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(c => storeScopeIds.Contains(c.LoanContract.StoreId));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(c =>
                    c.ChargeName.ToLower().Contains(keyword) ||
                    c.ChargeCode.ToLower().Contains(keyword) ||
                    c.StatusCode.ToLower().Contains(keyword));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "duedate") switch
            {
                "chargedate"  => sortDesc ? query.OrderByDescending(c => c.ChargeDate)  : query.OrderBy(c => c.ChargeDate),
                "statuscode"  => sortDesc ? query.OrderByDescending(c => c.StatusCode)  : query.OrderBy(c => c.StatusCode),
                "amount"      => sortDesc ? query.OrderByDescending(c => c.Amount)      : query.OrderBy(c => c.Amount),
                "createdat"   => sortDesc ? query.OrderByDescending(c => c.CreatedAt)   : query.OrderBy(c => c.CreatedAt),
                _             => sortDesc ? query.OrderByDescending(c => c.DueDate)     : query.OrderBy(c => c.DueDate)
            };

            var items = await sorted.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new { Total = total, PageIndex = pageIndex, PageSize = pageSize, Items = items };
        }
    }
}
