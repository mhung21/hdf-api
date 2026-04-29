using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface IInsuranceContractService : IBaseService<InsuranceContract>
    {
        /// <summary>Tạo mới hoặc cập nhật hợp đồng bảo hiểm.</summary>
        Task<InsuranceContract> Save(CUInsuranceContractModel model);

        /// <summary>Lấy danh sách hợp đồng bảo hiểm theo quyền.</summary>
        Task<IList<InsuranceContract>> GetAlls();

        /// <summary>Tìm kiếm hợp đồng bảo hiểm với phân trang và sắp xếp.</summary>
        Task<object> SearchInsuranceContract(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);
    }

    public class InsuranceContractService : BaseService<InsuranceContract, CrediflowContext>, IInsuranceContractService
    {
        public InsuranceContractService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<InsuranceContract>> GetAlls()
        {
            var query = DbContext.InsuranceContracts.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(i => storeScopeIds.Contains(i.LoanContract.StoreId));

            return await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
        }

        public async Task<InsuranceContract> Save(CUInsuranceContractModel model)
        {
            bool isCreate = model.InsuranceContractId == null || model.InsuranceContractId == Guid.Empty;
            InsuranceContract obj;

            if (isCreate)
            {
                obj = new InsuranceContract { InsuranceContractId = Guid.CreateVersion7() };
                DbContext.InsuranceContracts.Add(obj);
            }
            else
            {
                obj = await DbContext.InsuranceContracts.FindAsync(model.InsuranceContractId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy hợp đồng bảo hiểm với Id = {model.InsuranceContractId}");
            }

            obj.LoanContractId  = model.LoanContractId;
            obj.ProviderName    = model.ProviderName;
            obj.PolicyNumber    = model.PolicyNumber    ?? obj.PolicyNumber;
            obj.InsuranceName   = model.InsuranceName   ?? obj.InsuranceName;
            obj.IsMandatory     = model.IsMandatory;
            obj.PremiumAmount   = model.PremiumAmount;
            obj.CoverageAmount  = model.CoverageAmount  ?? obj.CoverageAmount;
            obj.EffectiveFrom   = model.EffectiveFrom;
            obj.EffectiveTo     = model.EffectiveTo     ?? obj.EffectiveTo;
            obj.StatusCode      = model.StatusCode;
            obj.Note            = model.Note            ?? obj.Note;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        public async Task<object> SearchInsuranceContract(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = DbContext.InsuranceContracts.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(i => storeScopeIds.Contains(i.LoanContract.StoreId));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(i =>
                    i.ProviderName.ToLower().Contains(keyword) ||
                    (i.PolicyNumber   != null && i.PolicyNumber.ToLower().Contains(keyword)) ||
                    (i.InsuranceName  != null && i.InsuranceName.ToLower().Contains(keyword)) ||
                    i.StatusCode.ToLower().Contains(keyword));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "createdat") switch
            {
                "providername"   => sortDesc ? query.OrderByDescending(i => i.ProviderName)   : query.OrderBy(i => i.ProviderName),
                "statuscode"     => sortDesc ? query.OrderByDescending(i => i.StatusCode)     : query.OrderBy(i => i.StatusCode),
                "effectivefrom"  => sortDesc ? query.OrderByDescending(i => i.EffectiveFrom)  : query.OrderBy(i => i.EffectiveFrom),
                "premiumamount"  => sortDesc ? query.OrderByDescending(i => i.PremiumAmount)  : query.OrderBy(i => i.PremiumAmount),
                _                => sortDesc ? query.OrderByDescending(i => i.CreatedAt)      : query.OrderBy(i => i.CreatedAt)
            };

            var items = await sorted.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new { Total = total, PageIndex = pageIndex, PageSize = pageSize, Items = items };
        }
    }
}
