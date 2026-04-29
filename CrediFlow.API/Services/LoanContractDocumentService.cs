using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ILoanContractDocumentService : IBaseService<LoanContractDocument>
    {
        Task<IList<LoanContractDocument>> GetByLoanContract(Guid loanContractId);
        Task<LoanContractDocument> Upload(Guid loanContractId, IFormFile file, string documentType, string? note);
        Task<(Stream stream, LoanContractDocument meta)> GetFileForStream(Guid documentId);
        Task Delete(Guid documentId);
    }

    public class LoanContractDocumentService : BaseService<LoanContractDocument, CrediflowContext>, ILoanContractDocumentService
    {
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "application/pdf"
        };

        private static readonly Dictionary<string, string> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "image/jpeg",      ".jpg"  },
            { "image/png",       ".png"  },
            { "image/webp",      ".webp" },
            { "application/pdf", ".pdf"  },
        };

        private readonly string _basePath;

        public LoanContractDocumentService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user)
        {
            _basePath = !string.IsNullOrEmpty(Config.FileStorage?.BasePath)
                ? Config.FileStorage.BasePath
                : Path.Combine(AppContext.BaseDirectory, "storage", "contracts");
            Directory.CreateDirectory(_basePath);
        }

        public async Task<IList<LoanContractDocument>> GetByLoanContract(Guid loanContractId)
        {
            var loan = await DbContext.LoanContracts.FindAsync(loanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy khoản vay với Id = {loanContractId}");

            if (!User.IsAdmin && loan.StoreId != User.StoreId)
                throw new UnauthorizedAccessException("Không có quyền xem khoản vay thuộc chi nhánh khác.");

            return await DbContext.LoanContractDocuments
                .Where(d => d.LoanContractId == loanContractId)
                .OrderBy(d => d.DocumentType)
                .ThenBy(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<LoanContractDocument> Upload(Guid loanContractId, IFormFile file, string documentType, string? note)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File không hợp lệ hoặc rỗng.");

            // Xác định content type (fallback bằng extension nếu browser gửi không đúng)
            var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedContentTypes.Contains(contentType))
            {
                contentType = Path.GetExtension(file.FileName).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png"            => "image/png",
                    ".webp"           => "image/webp",
                    ".pdf"            => "application/pdf",
                    _                 => contentType,
                };
            }

            if (!AllowedContentTypes.Contains(contentType))
                throw new ArgumentException("Chỉ chấp nhận file ảnh (JPEG, PNG, WebP) hoặc PDF.");

            long maxBytes = Config.FileStorage?.MaxFileSizeBytes > 0
                ? Config.FileStorage.MaxFileSizeBytes
                : 20 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new ArgumentException($"File vượt quá kích thước tối đa ({maxBytes / 1024 / 1024} MB).");

            var loan = await DbContext.LoanContracts.FindAsync(loanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy khoản vay với Id = {loanContractId}");

            if (!User.IsAdmin && loan.StoreId != User.StoreId)
                throw new UnauthorizedAccessException("Không có quyền upload file cho khoản vay thuộc chi nhánh khác.");

            var documentId = Guid.CreateVersion7();
            var fileExt    = ExtensionMap.TryGetValue(contentType, out var ext) ? ext : Path.GetExtension(file.FileName);
            var docDir     = Path.Combine(_basePath, loanContractId.ToString(), "docs");
            Directory.CreateDirectory(docDir);

            // Tên file lưu an toàn: chỉ dùng GUID, không dùng tên gốc làm đường dẫn (phòng path traversal)
            var safeFileName = $"{documentId}{fileExt}";
            var absolutePath = Path.Combine(docDir, safeFileName);
            var relativePath = Path.Combine(loanContractId.ToString(), "docs", safeFileName);

            await using (var fs = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
                await file.CopyToAsync(fs);

            var document = new LoanContractDocument
            {
                DocumentId     = documentId,
                LoanContractId = loanContractId,
                DocumentType   = documentType,
                FileName       = file.FileName,          // tên hiển thị gốc
                FileSize       = file.Length,
                ContentType    = contentType,
                StoragePath    = relativePath,
                UploadedBy     = CommonLib.GetGUID(User.UserId),
                Note           = string.IsNullOrWhiteSpace(note) ? null : note,
                UploadedAt     = DateTime.Now,
            };

            DbContext.LoanContractDocuments.Add(document);
            await DbContext.SaveChangesAsync();
            return document;
        }

        public async Task<(Stream stream, LoanContractDocument meta)> GetFileForStream(Guid documentId)
        {
            var result = await DbContext.LoanContractDocuments
                .Where(d => d.DocumentId == documentId)
                .Select(d => new { Meta = d, ContractStoreId = d.LoanContract.StoreId })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Không tìm thấy file giấy tờ.");

            if (!User.IsAdmin && result.ContractStoreId != User.StoreId)
                throw new UnauthorizedAccessException("Không có quyền truy cập file này.");

            var doc = result.Meta;
            var fullPath = Path.Combine(_basePath, doc.StoragePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File vật lý không tồn tại: {doc.StoragePath}");

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, doc);
        }

        public async Task Delete(Guid documentId)
        {
            var result = await DbContext.LoanContractDocuments
                .Where(d => d.DocumentId == documentId)
                .Select(d => new { Meta = d, ContractStoreId = d.LoanContract.StoreId, ContractStatus = d.LoanContract.StatusCode })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Không tìm thấy file giấy tờ.");

            if (!User.IsAdmin && result.ContractStoreId != User.StoreId)
                throw new UnauthorizedAccessException("Không có quyền xóa file thuộc chi nhánh khác.");

            if (result.ContractStatus != "DRAFT")
                throw new InvalidOperationException("Chỉ được xóa giấy tờ khi khoản vay ở trạng thái DRAFT.");

            var doc = result.Meta;
            // Xóa file vật lý trước
            var fullPath = Path.Combine(_basePath, doc.StoragePath);
            if (File.Exists(fullPath))
            {
                try { File.Delete(fullPath); } catch { /* bỏ qua nếu file đã bị xóa ngoài hệ thống */ }
            }

            DbContext.LoanContractDocuments.Remove(doc);
            await DbContext.SaveChangesAsync();
        }
    }
}
