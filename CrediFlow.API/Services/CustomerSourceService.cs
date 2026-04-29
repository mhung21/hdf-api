using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ICustomerSourceService : IBaseService<CustomerSource>
    {
        /// <summary>Lấy danh sách tất cả luồng khách (đang hoạt động), sắp xếp theo sort_order.</summary>
        Task<IList<CustomerSource>> GetAlls();

        /// <summary>Tạo mới hoặc cập nhật luồng khách.</summary>
        Task<CustomerSource> Save(CUCustomerSourceModel model);

        /// <summary>Xóa luồng khách (hard-delete nếu chưa có hợp đồng liên kết).</summary>
        Task Delete(Guid sourceId);
    }

    public class CustomerSourceService : BaseService<CustomerSource, CrediflowContext>, ICustomerSourceService
    {
        public CustomerSourceService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        public async Task<IList<CustomerSource>> GetAlls()
        {
            return await DbContext.CustomerSources
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.SourceName)
                .ToListAsync();
        }

        public async Task<CustomerSource> Save(CUCustomerSourceModel model)
        {
            bool isCreate = model.SourceId == null || model.SourceId == Guid.Empty;
            CustomerSource obj;

            if (isCreate)
            {
                obj = new CustomerSource
                {
                    SourceId  = Guid.CreateVersion7(),
                    CreatedBy = User.UserId,
                    CreatedAt = DateTime.UtcNow,
                };
                DbContext.CustomerSources.Add(obj);
            }
            else
            {
                obj = await DbContext.CustomerSources.FindAsync(model.SourceId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy luồng khách với Id = {model.SourceId}");
            }

            // Kiểm tra trùng tên
            bool isDuplicate = await DbContext.CustomerSources
                .AnyAsync(s => s.SourceName == model.SourceName && s.SourceId != obj.SourceId);
            if (isDuplicate)
                throw new InvalidOperationException($"Tên luồng khách '{model.SourceName}' đã tồn tại.");

            obj.SourceName = model.SourceName;
            obj.IsActive   = model.IsActive;
            obj.SortOrder  = model.SortOrder;
            obj.UpdatedAt  = DateTime.UtcNow;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        public async Task Delete(Guid sourceId)
        {
            var obj = await DbContext.CustomerSources.FindAsync(sourceId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy luồng khách với Id = {sourceId}");

            // Kiểm tra xem có hợp đồng vay nào đang dùng luồng khách này không
            bool hasContracts = await DbContext.LoanContracts.AnyAsync(c => c.CustomerSourceId == sourceId);
            if (hasContracts)
                throw new InvalidOperationException(
                    "Không thể xóa luồng khách đã được sử dụng trong hợp đồng vay. " +
                    "Bạn có thể ẩn luồng khách bằng cách tắt trạng thái 'Hoạt động'.");

            DbContext.CustomerSources.Remove(obj);
            await DbContext.SaveChangesAsync();
        }
    }
}
