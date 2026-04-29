using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ICustomerDocumentService : IBaseService<CustomerDocument>
    {
        /// <summary>Lấy danh sách ảnh CCCD theo khách hàng. Chỉ Admin/Manager mới được xem.</summary>
        Task<IList<CustomerDocument>> GetByCustomer(Guid customerId);

        /// <summary>Upload ảnh CCCD / giấy tờ cho khách hàng. Mọi vai trò đều có thể upload.</summary>
        Task<CustomerDocument> Upload(Guid customerId, IFormFile file, string documentType, string? note);

        /// <summary>Trả về stream file + metadata. Chỉ Admin/Manager mới được xem.</summary>
        Task<(Stream stream, CustomerDocument meta)> GetFileForStream(Guid documentId);
    }

    public class CustomerDocumentService : BaseService<CustomerDocument, CrediflowContext>, ICustomerDocumentService
    {
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp"
        };

        private static readonly Dictionary<string, string> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "image/jpeg", ".jpg"  },
            { "image/png",  ".png"  },
            { "image/webp", ".webp" },
        };

        private readonly string _basePath;

        public CustomerDocumentService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user)
        {
            _basePath = !string.IsNullOrEmpty(Config.FileStorage?.BasePath)
                ? Config.FileStorage.BasePath
                : Path.Combine(AppContext.BaseDirectory, "storage", "contracts");
            Directory.CreateDirectory(_basePath);
        }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<CustomerDocument>> GetByCustomer(Guid customerId)
        {
            // Chỉ Admin / StoreManager mới được xem ảnh CCCD
            if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
                throw new UnauthorizedAccessException("Chỉ quản lý cửa hàng hoặc admin mới được xem ảnh CCCD của khách hàng.");

            var customer = await DbContext.Customers.FindAsync(customerId)
                ?? throw new KeyNotFoundException($"Không tìm thấy khách hàng với Id = {customerId}");

            if (!User.IsAdmin)
            {
                var storeScopeIds = GetStoreScopeIds();
                if (storeScopeIds is not null && (!customer.FirstStoreId.HasValue || !storeScopeIds.Contains(customer.FirstStoreId.Value)))
                    throw new UnauthorizedAccessException("Không có quyền xem tài liệu khách hàng thuộc chi nhánh khác.");
            }

            return await DbContext.CustomerDocuments
                .Where(d => d.CustomerId == customerId)
                .OrderBy(d => d.DocumentType)
                .ThenBy(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<CustomerDocument> Upload(Guid customerId, IFormFile file, string documentType, string? note)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File không hợp lệ hoặc rỗng.");

            var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedContentTypes.Contains(contentType))
            {
                contentType = Path.GetExtension(file.FileName).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png"            => "image/png",
                    ".webp"           => "image/webp",
                    _                 => contentType,
                };
            }

            if (!AllowedContentTypes.Contains(contentType))
                throw new ArgumentException("Ảnh CCCD chỉ chấp nhận định dạng JPEG, PNG hoặc WebP.");

            long maxBytes = Config.FileStorage?.MaxFileSizeBytes > 0
                ? Config.FileStorage.MaxFileSizeBytes
                : 20 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new ArgumentException($"File vượt quá kích thước tối đa ({maxBytes / 1024 / 1024} MB).");

            var customer = await DbContext.Customers.FindAsync(customerId)
                ?? throw new KeyNotFoundException($"Không tìm thấy khách hàng với Id = {customerId}");

            // Mọi vai trò đều có thể upload, nhưng chỉ được upload cho KH thuộc chi nhánh mình
            if (!User.IsAdmin)
            {
                var storeScopeIds = GetStoreScopeIds();
                if (storeScopeIds is not null && (!customer.FirstStoreId.HasValue || !storeScopeIds.Contains(customer.FirstStoreId.Value)))
                    throw new UnauthorizedAccessException("Không có quyền upload tài liệu cho khách hàng thuộc chi nhánh khác.");
            }

            var documentId = Guid.CreateVersion7();
            var fileExt    = ExtensionMap.TryGetValue(contentType, out var ext) ? ext : Path.GetExtension(file.FileName);
            var docDir     = Path.Combine(_basePath, "customers", customerId.ToString());
            Directory.CreateDirectory(docDir);

            // Tên file an toàn: chỉ dùng GUID + extension, không dùng tên gốc (phòng path traversal)
            var safeFileName = $"{documentId}{fileExt}";
            var absolutePath = Path.Combine(docDir, safeFileName);
            var relativePath = Path.Combine("customers", customerId.ToString(), safeFileName);

            await using (var fs = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
                await file.CopyToAsync(fs);

            var document = new CustomerDocument
            {
                DocumentId   = documentId,
                CustomerId   = customerId,
                DocumentType = documentType,
                FileName     = file.FileName,
                FileSize     = file.Length,
                ContentType  = contentType,
                StoragePath  = relativePath,
                UploadedBy   = CommonLib.GetGUID(User.UserId),
                Note         = string.IsNullOrWhiteSpace(note) ? null : note,
                UploadedAt   = DateTime.Now,
            };

            DbContext.CustomerDocuments.Add(document);
            await DbContext.SaveChangesAsync();
            return document;
        }

        public async Task<(Stream stream, CustomerDocument meta)> GetFileForStream(Guid documentId)
        {
            // Chỉ Admin / StoreManager mới được xem ảnh CCCD
            if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
                throw new UnauthorizedAccessException("Chỉ quản lý cửa hàng hoặc admin mới được xem ảnh CCCD của khách hàng.");

            var result = await DbContext.CustomerDocuments
                .Where(d => d.DocumentId == documentId)
                .Select(d => new { Meta = d, CustomerStoreId = d.Customer.FirstStoreId })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Không tìm thấy file giấy tờ khách hàng.");

            if (!User.IsAdmin)
            {
                var storeScopeIds = GetStoreScopeIds();
                if (storeScopeIds is not null && (!result.CustomerStoreId.HasValue || !storeScopeIds.Contains(result.CustomerStoreId.Value)))
                    throw new UnauthorizedAccessException("Không có quyền truy cập file này.");
            }

            var doc = result.Meta;
            var fullPath = Path.Combine(_basePath, doc.StoragePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File vật lý không tồn tại: {doc.StoragePath}");

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, doc);
        }
    }
}
