using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface IPolicySettingService : IBaseService<PolicySetting>
    {
        /// <summary>Tạo mới hoặc cập nhật chính sách phí.</summary>
        Task<PolicySetting> Save(CUPolicySettingModel model);

        /// <summary>Lấy danh sách chính sách phí theo quyền (kèm thông tin cửa hàng).</summary>
        Task<IList<object>> GetAlls();

        /// <summary>Tìm kiếm chính sách phí với phân trang và sắp xếp.</summary>
        Task<object> SearchPolicySetting(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);

        /// <summary>
        /// Lấy chính sách đang áp dụng cho một cửa hàng tại một ngày cụ thể.
        /// Ưu tiên: chính sách riêng cửa hàng → chính sách toàn hệ thống.
        /// Khi cùng phạm vi, lấy chính sách có effective_from gần nhất (latest wins).
        /// </summary>
        Task<PolicySetting?> GetActivePolicy(Guid storeId, DateOnly date);
    }

    public class PolicySettingService : BaseService<PolicySetting, CrediflowContext>, IPolicySettingService
    {
        public PolicySettingService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<object>> GetAlls()
        {
            var query = DbContext.PolicySettings
                .Include(p => p.Stores)
                .AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
            {
                query = query.Where(p =>
                    !p.Stores.Any()
                    || p.Stores.Any(s => storeScopeIds.Contains(s.StoreId)));
            }

            var list = await query
                .OrderByDescending(p => p.EffectiveFrom)
                .ToListAsync();

            return list.Select(MapToResponse).ToList<object>();
        }

        public async Task<PolicySetting> Save(CUPolicySettingModel model)
        {
            bool isCreate = model.PolicyId == null || model.PolicyId == Guid.Empty;
            PolicySetting obj;

            if (isCreate)
            {
                obj = new PolicySetting
                {
                    PolicyId  = Guid.CreateVersion7(),
                    CreatedBy = CommonLib.GetGUID(User.UserId),
                };
                DbContext.PolicySettings.Add(obj);
            }
            else
            {
                obj = await DbContext.PolicySettings
                    .Include(p => p.Stores)
                    .FirstOrDefaultAsync(p => p.PolicyId == model.PolicyId!.Value)
                      ?? throw new KeyNotFoundException($"Không tìm thấy chính sách với Id = {model.PolicyId}");

                // Xóa liên kết cửa hàng cũ để ghi đè bằng danh sách mới
                var joinSet = DbContext.Set<Dictionary<string, object>>("PolicySettingStore");
                var existing = await joinSet
                    .Where(j => EF.Property<Guid>(j, "PolicyId") == obj.PolicyId)
                    .ToListAsync();
                joinSet.RemoveRange(existing);
            }

            obj.EffectiveFrom              = model.EffectiveFrom;
            obj.EffectiveTo                = model.EffectiveTo;
            obj.EarlySettlementPenaltyRate = model.EarlySettlementPenaltyRate;
            obj.LatePaymentPenaltyRate     = model.LatePaymentPenaltyRate;
            obj.LatePaymentStartDay        = model.LatePaymentStartDay;
            obj.BadDebtStartDay            = model.BadDebtStartDay;
            obj.WarningDays                = model.WarningDays;
            obj.InsuranceDiscountRate      = model.InsuranceDiscountRate;

            // Thêm liên kết cửa hàng mới (distinct để tránh trùng)
            var joinSet2 = DbContext.Set<Dictionary<string, object>>("PolicySettingStore");
            foreach (var storeId in model.StoreIds.Distinct())
            {
                joinSet2.Add(new Dictionary<string, object>
                {
                    ["PolicyId"] = obj.PolicyId,
                    ["StoreId"]  = storeId,
                });
            }

            await DbContext.SaveChangesAsync();
            return obj;
        }

        public async Task<object> SearchPolicySetting(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize  = Math.Clamp(pageSize, 1, 200);

            var query = DbContext.PolicySettings
                .Include(p => p.Stores)
                .AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(p =>
                    !p.Stores.Any()
                    || p.Stores.Any(s => storeScopeIds.Contains(s.StoreId)));

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "effectivefrom") switch
            {
                "effectiveto" => sortDesc ? query.OrderByDescending(p => p.EffectiveTo)   : query.OrderBy(p => p.EffectiveTo),
                "createdat"   => sortDesc ? query.OrderByDescending(p => p.CreatedAt)     : query.OrderBy(p => p.CreatedAt),
                _             => sortDesc ? query.OrderByDescending(p => p.EffectiveFrom) : query.OrderBy(p => p.EffectiveFrom),
            };

            var items = await sorted
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new
            {
                Total     = total,
                PageIndex = pageIndex,
                PageSize  = pageSize,
                Items     = items.Select(MapToResponse).ToList(),
            };
        }

        public async Task<PolicySetting?> GetActivePolicy(Guid storeId, DateOnly date)
        {
            // 1. Ưu tiên chính sách riêng cho cửa hàng
            var storePolicy = await DbContext.PolicySettings
                .Where(p => p.Stores.Any(s => s.StoreId == storeId))
                .Where(p => p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo >= date))
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (storePolicy != null) return storePolicy;

            // 2. Fallback: chính sách toàn hệ thống (không gắn cửa hàng nào)
            return await DbContext.PolicySettings
                .Where(p => !p.Stores.Any())
                .Where(p => p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo >= date))
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();
        }

        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        private static object MapToResponse(PolicySetting p) => new
        {
            p.PolicyId,
            p.EffectiveFrom,
            p.EffectiveTo,
            p.EarlySettlementPenaltyRate,
            p.LatePaymentPenaltyRate,
            p.LatePaymentStartDay,
            p.BadDebtStartDay,
            p.WarningDays,
            p.InsuranceDiscountRate,
            p.CreatedAt,
            IsGlobal   = !p.Stores.Any(),
            StoreIds   = p.Stores.Select(s => s.StoreId).ToList(),
            StoreNames = p.Stores.Select(s => s.StoreName).ToList(),
        };
    }
}
