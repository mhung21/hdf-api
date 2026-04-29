using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface IStoreService : IBaseService<Store>
    {
        /// <summary>Tạo mới hoặc cập nhật chi nhánh.</summary>
        Task<Store> Save(CUStoreModel model);

        /// <summary>Lấy danh sách chi nhánh theo quyền.</summary>
        Task<IList<Store>> GetAlls();

        /// <summary>Tìm kiếm chi nhánh với phân trang và sắp xếp.</summary>
        Task<object> SearchStore(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);

        /// <summary>Khóa ngày làm việc: nhân viên không được sửa dữ liệu ngày đó sau khi khóa.</summary>
        Task<StoreDayLock> LockDay(Guid storeId, DateOnly businessDate, string? note);

        /// <summary>Mở khóa ngày (chỉ Admin mới được).</summary>
        Task<StoreDayLock> UnlockDay(Guid storeId, DateOnly businessDate);
    }

    public class StoreService : BaseService<Store, CrediflowContext>, IStoreService
    {
        public StoreService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        // ──────────────────────────────────────────────────────────────────────
        // GetAlls
        // ──────────────────────────────────────────────────────────────────────

        public async Task<IList<Store>> GetAlls()
        {
            var query = DbContext.Stores.AsQueryable();

            // Nhân viên / StoreManager chỉ thấy chi nhánh của mình
            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(s => storeScopeIds.Contains(s.StoreId));

            return await query.OrderBy(s => s.StoreName).ToListAsync();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Save
        // ──────────────────────────────────────────────────────────────────────

        public async Task<Store> Save(CUStoreModel model)
        {
            bool isCreate = model.StoreId == null || model.StoreId == Guid.Empty;
            Store obj;

            if (isCreate)
            {
                obj = new Store { StoreId = Guid.CreateVersion7() };
                DbContext.Stores.Add(obj);
            }
            else
            {
                obj = await DbContext.Stores.FindAsync(model.StoreId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy chi nhánh với Id = {model.StoreId}");
            }

            obj.StoreCode  = model.StoreCode;
            obj.StoreName  = model.StoreName;
            obj.Address    = model.Address    ?? obj.Address;
            obj.Phone      = model.Phone      ?? obj.Phone;
            obj.OpenedOn   = model.OpenedOn   ?? obj.OpenedOn;
            obj.IsActive   = model.IsActive;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        // ──────────────────────────────────────────────────────────────────────
        // SearchStore
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> SearchStore(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = DbContext.Stores.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(s => storeScopeIds.Contains(s.StoreId));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(s =>
                    s.StoreName.ToLower().Contains(keyword) ||
                    s.StoreCode.ToLower().Contains(keyword) ||
                    (s.Address != null && s.Address.ToLower().Contains(keyword)) ||
                    (s.Phone   != null && s.Phone.Contains(keyword)));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "storename") switch
            {
                "storecode" => sortDesc ? query.OrderByDescending(s => s.StoreCode) : query.OrderBy(s => s.StoreCode),
                "isactive"  => sortDesc ? query.OrderByDescending(s => s.IsActive)  : query.OrderBy(s => s.IsActive),
                "createdat" => sortDesc ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
                _           => sortDesc ? query.OrderByDescending(s => s.StoreName) : query.OrderBy(s => s.StoreName)
            };

            var items = await sorted.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

            return new { Total = total, PageIndex = pageIndex, PageSize = pageSize, Items = items };
        }

        // ──────────────────────────────────────────────────────────────────────
        // LockDay / UnlockDay
        // ──────────────────────────────────────────────────────────────────────

        public async Task<StoreDayLock> LockDay(Guid storeId, DateOnly businessDate, string? note)
        {
            var existing = await DbContext.StoreDayLocks
                .FirstOrDefaultAsync(l => l.StoreId == storeId && l.BusinessDate == businessDate);

            if (existing != null)
            {
                existing.IsLocked  = true;
                existing.LockedAt  = DateTime.Now;
                existing.LockedBy  = CommonLib.GetGUID(User.UserId);
                existing.Note      = note ?? existing.Note;
            }
            else
            {
                existing = new StoreDayLock
                {
                    StoreId      = storeId,
                    BusinessDate = businessDate,
                    IsLocked     = true,
                    LockedAt     = DateTime.Now,
                    LockedBy     = CommonLib.GetGUID(User.UserId),
                    Note         = note,
                };
                DbContext.StoreDayLocks.Add(existing);
            }

            await DbContext.SaveChangesAsync();
            return existing;
        }

        public async Task<StoreDayLock> UnlockDay(Guid storeId, DateOnly businessDate)
        {
            var existing = await DbContext.StoreDayLocks
                .FirstOrDefaultAsync(l => l.StoreId == storeId && l.BusinessDate == businessDate)
                ?? throw new KeyNotFoundException($"Không tìm thấy bản ghi khóa ngày {businessDate:dd/MM/yyyy} cho cửa hàng.");

            existing.IsLocked = false;
            await DbContext.SaveChangesAsync();
            return existing;
        }
    }
}
