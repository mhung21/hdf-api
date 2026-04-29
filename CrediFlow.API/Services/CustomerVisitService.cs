using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ICustomerVisitService : IBaseService<CustomerVisit>
    {
        /// <summary>Tạo mới hoặc cập nhật lượt đến của khách hàng.</summary>
        Task<CustomerVisit> Save(CUCustomerVisitModel model);

        /// <summary>Lấy danh sách lượt đến theo quyền.</summary>
        Task<IList<CustomerVisit>> GetAlls();

        /// <summary>Tìm kiếm lượt đến với phân trang và sắp xếp.</summary>
        Task<object> SearchCustomerVisit(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);
    }

    public class CustomerVisitService : BaseService<CustomerVisit, CrediflowContext>, ICustomerVisitService
    {
        public CustomerVisitService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<CustomerVisit>> GetAlls()
        {
            var query = DbContext.CustomerVisits.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(v => storeScopeIds.Contains(v.StoreId));

            return await query.OrderByDescending(v => v.VisitDate).ToListAsync();
        }

        public async Task<CustomerVisit> Save(CUCustomerVisitModel model)
        {
            bool isCreate = model.VisitId == null || model.VisitId == Guid.Empty;
            CustomerVisit obj;

            if (isCreate)
            {
                obj = new CustomerVisit { VisitId = Guid.CreateVersion7() };
                DbContext.CustomerVisits.Add(obj);
            }
            else
            {
                obj = await DbContext.CustomerVisits.FindAsync(model.VisitId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy lượt đến với Id = {model.VisitId}");
            }

            obj.StoreId    = model.StoreId;
            obj.CustomerId = model.CustomerId;
            obj.VisitDate  = model.VisitDate;
            obj.VisitType  = model.VisitType;
            obj.SourceType = model.SourceType ?? obj.SourceType;
            obj.HandledBy  = model.HandledBy  ?? obj.HandledBy;
            obj.Note       = model.Note       ?? obj.Note;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        public async Task<object> SearchCustomerVisit(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = DbContext.CustomerVisits.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(v => storeScopeIds.Contains(v.StoreId));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(v =>
                    v.VisitType.ToLower().Contains(keyword) ||
                    (v.Note != null && v.Note.ToLower().Contains(keyword)));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "visitdate") switch
            {
                "visittype"  => sortDesc ? query.OrderByDescending(v => v.VisitType) : query.OrderBy(v => v.VisitType),
                "sourcetype" => sortDesc ? query.OrderByDescending(v => v.SourceType): query.OrderBy(v => v.SourceType),
                "createdat"  => sortDesc ? query.OrderByDescending(v => v.CreatedAt) : query.OrderBy(v => v.CreatedAt),
                _            => sortDesc ? query.OrderByDescending(v => v.VisitDate) : query.OrderBy(v => v.VisitDate)
            };

            var items = await sorted.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new { Total = total, PageIndex = pageIndex, PageSize = pageSize, Items = items };
        }
    }
}
