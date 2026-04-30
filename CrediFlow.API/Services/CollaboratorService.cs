using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ICollaboratorService : IBaseService<Collaborator>
    {
        /// <summary>Tạo mới hoặc cập nhật CTV tùy theo <see cref="CUCollaboratorModel.CollaboratorId"/>.</summary>
        Task<Collaborator> Save(CUCollaboratorModel model);

        /// <summary>Lấy danh sách CTV theo quyền.</summary>
        Task<IList<Collaborator>> GetAlls();

        /// <summary>Tìm kiếm CTV với phân trang.</summary>
        Task<object> SearchCollaborator(string keyword, int pageIndex, int pageSize);

        /// <summary>Báo cáo hoa hồng: tổng hợp số hợp đồng phát sinh và số tiền hoa hồng cần trả cho từng CTV.</summary>
        Task<object> GetCommissionReport(DateOnly? fromDate, DateOnly? toDate, Guid? storeId, int pageIndex, int pageSize);
    }

    public class CollaboratorService : BaseService<Collaborator, CrediflowContext>, ICollaboratorService
    {
        public CollaboratorService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        // ──────────────────────────────────────────────────────────────────────
        // GetAlls
        // ──────────────────────────────────────────────────────────────────────

        public async Task<IList<Collaborator>> GetAlls()
        {
            var query = DbContext.Collaborators.AsQueryable();

            // Admin thấy tất cả; còn lại chỉ thấy CTV của chi nhánh mình
            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(c => storeScopeIds.Any(id => id == c.StoreId));

            return await query
                .Where(c => c.IsActive)
                .OrderBy(c => c.FullName)
                .ToListAsync();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Save (Create / Update)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<Collaborator> Save(CUCollaboratorModel model)
        {
            bool isCreate = model.CollaboratorId == null || model.CollaboratorId == Guid.Empty;
            Collaborator obj;

            if (isCreate)
            {
                obj = new Collaborator
                {
                    CollaboratorId = Guid.CreateVersion7(),
                    CreatedBy = CommonLib.GetGUID(User.UserId),
                    CreatedAt = DateTime.Now,
                };
                DbContext.Collaborators.Add(obj);
            }
            else
            {
                obj = await DbContext.Collaborators.FindAsync(model.CollaboratorId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy CTV với Id = {model.CollaboratorId}");
            }

            obj.FullName = model.FullName;
            obj.Phone = model.Phone ?? obj.Phone;
            obj.IdNumber = model.IdNumber ?? obj.IdNumber;
            obj.StoreId = model.StoreId ?? obj.StoreId;
            obj.Note = model.Note ?? obj.Note;
            obj.CommissionRate = model.CommissionRate;
            obj.IsActive = model.IsActive;
            obj.UpdatedAt = DateTime.Now;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        // ──────────────────────────────────────────────────────────────────────
        // SearchCollaborator
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> SearchCollaborator(string keyword, int pageIndex, int pageSize)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;


            var query = DbContext.Collaborators
                .Include(c => c.Store)
                .AsQueryable();

            // Admin thấy tất cả; còn lại chỉ thấy CTV của chi nhánh mình
            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(c => storeScopeIds.Any(id => id == c.StoreId));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(c =>
                    c.FullName.ToLower().Contains(kw) ||
                    (c.Phone != null && c.Phone.Contains(kw)));
            }

            int total = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.FullName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.CollaboratorId,
                    c.FullName,
                    c.Phone,
                    c.IdNumber,
                    c.StoreId,
                    StoreName = c.Store != null ? c.Store.StoreName : null,
                    c.Note,
                    c.IsActive,
                    c.CommissionRate,
                    c.CreatedAt,
                })
                .ToListAsync();

            return new
            {
                Total = total,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Items = items,
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // GetCommissionReport
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> GetCommissionReport(DateOnly? fromDate, DateOnly? toDate, Guid? storeId, int pageIndex, int pageSize)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;

            // Lấy tất cả hợp đồng đã giải ngân (DISBURSED, SETTLED, CLOSED, BAD_DEBT, BAD_DEBT_CLOSED)
            // của khách hàng do CTV giới thiệu, trong khoảng thời gian lọc.
            var loanQuery = DbContext.LoanContracts
                .Include(l => l.Customer)
                    .ThenInclude(c => c.ReferredByCollaborator)
                .Where(l => l.Customer.ReferredByCollaboratorId != null)
                .Where(l => l.DisbursementDate != null)
                .Where(l => l.StatusCode != "DRAFT"
                         && l.StatusCode != "PENDING_APPROVAL"
                         && l.StatusCode != "PENDING_DISBURSEMENT"
                         && l.StatusCode != "CANCELLED");

            if (fromDate.HasValue)
                loanQuery = loanQuery.Where(l => l.DisbursementDate >= fromDate.Value);
            if (toDate.HasValue)
                loanQuery = loanQuery.Where(l => l.DisbursementDate <= toDate.Value);
            if (storeId.HasValue)
                loanQuery = loanQuery.Where(l => l.StoreId == storeId.Value);

            // Admin thấy tất cả; còn lại chỉ thấy chi nhánh mình
            var storeScopeIds = GetStoreScopeIds(storeId);
            if (storeScopeIds is not null)
                loanQuery = loanQuery.Where(l => storeScopeIds.Any(id => id == l.StoreId));

            // Nhóm theo CTV
            var grouped = await loanQuery
                .GroupBy(l => l.Customer.ReferredByCollaboratorId)
                .Select(g => new
                {
                    CollaboratorId  = g.Key,
                    LoanCount       = g.Count(),
                    TotalPrincipal  = g.Sum(l => l.PrincipalAmount),
                })
                .ToListAsync();

            // Lấy thông tin CTV cho các ID trên
            var ctvIds = grouped.Select(g => g.CollaboratorId!.Value).ToList();
            var ctvQuery = DbContext.Collaborators
                .Include(c => c.Store)
                .Where(c => ctvIds.Contains(c.CollaboratorId));

            if (storeScopeIds is not null)
                ctvQuery = ctvQuery.Where(c => storeScopeIds.Any(id => id == c.StoreId));

            var ctvMap = await ctvQuery
                .ToDictionaryAsync(c => c.CollaboratorId);

            var rows = grouped
                .Where(g => ctvMap.ContainsKey(g.CollaboratorId!.Value))
                .Select(g =>
                {
                    var ctv = ctvMap[g.CollaboratorId!.Value];
                    var rate = ctv.CommissionRate ?? 0m;
                    return new
                    {
                        ctv.CollaboratorId,
                        ctv.FullName,
                        ctv.Phone,
                        ctv.IdNumber,
                        StoreName      = ctv.Store != null ? ctv.Store.StoreName : null,
                        ctv.CommissionRate,
                        g.LoanCount,
                        g.TotalPrincipal,
                        CommissionAmount = Math.Round(g.TotalPrincipal * rate / 100, 0),
                    };
                })
                .OrderByDescending(r => r.CommissionAmount)
                .ToList();

            int total = rows.Count;
            var items = rows.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

            return new
            {
                TotalCount = total,
                PageIndex  = pageIndex,
                PageSize   = pageSize,
                Items      = items,
            };
        }
    }
}
