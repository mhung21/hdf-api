using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ILoanProductService : IBaseService<LoanProduct>
    {
        /// <summary>Tạo mới hoặc cập nhật sản phẩm vay.</summary>
        Task<LoanProduct> Save(CULoanProductModel model);

        /// <summary>Lấy danh sách sản phẩm vay theo quyền.</summary>
        Task<IList<LoanProduct>> GetAlls();

        /// <summary>Tìm kiếm sản phẩm vay với phân trang và sắp xếp.</summary>
        Task<object> SearchLoanProduct(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);
    }

    public class LoanProductService : BaseService<LoanProduct, CrediflowContext>, ILoanProductService
    {
        public LoanProductService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<LoanProduct>> GetAlls()
        {
            var query = DbContext.LoanProducts.AsQueryable();

            // Admin thấy tất cả; các role khác chỉ thấy sản phẩm chung (StoreId = null) + sản phẩm của chi nhánh mình
            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(p => p.StoreId == null || (p.StoreId.HasValue && storeScopeIds.Contains(p.StoreId.Value)));

            return await query.OrderBy(p => p.ProductName).ToListAsync();
        }

        public async Task<LoanProduct> Save(CULoanProductModel model)
        {
            bool isCreate = model.LoanProductId == null || model.LoanProductId == Guid.Empty;
            LoanProduct obj;

            if (isCreate)
            {
                obj = new LoanProduct { LoanProductId = Guid.CreateVersion7() };
                DbContext.LoanProducts.Add(obj);
            }
            else
            {
                obj = await DbContext.LoanProducts.FindAsync(model.LoanProductId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy sản phẩm vay với Id = {model.LoanProductId}");
            }

            obj.StoreId                   = model.StoreId;
            obj.ProductCode               = model.ProductCode;
            obj.ProductName               = model.ProductName;
            obj.Description               = model.Description               ?? obj.Description;
            obj.MinPrincipalAmount        = model.MinPrincipalAmount;
            obj.MaxPrincipalAmount        = model.MaxPrincipalAmount;
            obj.MinTermMonths             = model.MinTermMonths;
            obj.MaxTermMonths             = model.MaxTermMonths;
            obj.InterestRateMonthly       = model.InterestRateMonthly;
            obj.QlkvRateMonthly           = model.QlkvRateMonthly;
            obj.QltsRateMonthly           = model.QltsRateMonthly;
            obj.FixedMonthlyFeeAmount     = model.FixedMonthlyFeeAmount;
            obj.DefaultFileFeeAmount      = model.DefaultFileFeeAmount;
            obj.DefaultInsuranceRate      = model.DefaultInsuranceRate;
            obj.IsActive                  = model.IsActive;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        public async Task<object> SearchLoanProduct(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;


            var query = DbContext.LoanProducts.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(p => p.StoreId == null || (p.StoreId.HasValue && storeScopeIds.Contains(p.StoreId.Value)));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(p =>
                    p.ProductName.ToLower().Contains(keyword) ||
                    p.ProductCode.ToLower().Contains(keyword) ||
                    (p.Description != null && p.Description.ToLower().Contains(keyword)));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "productname") switch
            {
                "productcode" => sortDesc ? query.OrderByDescending(p => p.ProductCode) : query.OrderBy(p => p.ProductCode),
                "isactive"    => sortDesc ? query.OrderByDescending(p => p.IsActive)    : query.OrderBy(p => p.IsActive),
                "createdat"   => sortDesc ? query.OrderByDescending(p => p.CreatedAt)   : query.OrderBy(p => p.CreatedAt),
                _             => sortDesc ? query.OrderByDescending(p => p.ProductName) : query.OrderBy(p => p.ProductName)
            };

            var items = await sorted.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new { Total = total, PageIndex = pageIndex, PageSize = pageSize, Items = items };
        }
    }
}
