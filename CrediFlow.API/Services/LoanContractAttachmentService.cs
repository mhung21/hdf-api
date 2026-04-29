using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ILoanContractAttachmentService : IBaseService<LoanContractAttachment>
    {
        /// <summary>Upload file PDF hợp đồng vay. Mỗi khoản vay chỉ được 1 file.</summary>
        Task<LoanContractAttachment> Upload(Guid loanContractId, IFormFile file, string? note);

        /// <summary>Lấy metadata của file đính kèm theo khoản vay.</summary>
        Task<LoanContractAttachment?> GetByLoanContractId(Guid loanContractId);

        /// <summary>Mở stream file để phục vụ download / view. Bao gồm kiểm tra quyền truy cập.</summary>
        Task<(Stream stream, LoanContractAttachment meta)> GetFileForStream(Guid attachmentId);
    }

    public class LoanContractAttachmentService : BaseService<LoanContractAttachment, CrediflowContext>, ILoanContractAttachmentService
    {
        // Đường dẫn gốc lấy từ Config.FileStorage.BasePath (cấu hình trong appsettings.json)
        private readonly string _basePath;

        public LoanContractAttachmentService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user)
        {
            _basePath = !string.IsNullOrEmpty(Config.FileStorage?.BasePath)
                ? Config.FileStorage.BasePath
                : Path.Combine(AppContext.BaseDirectory, "storage", "contracts");

            // Tạo thư mục gốc nếu chưa tồn tại
            Directory.CreateDirectory(_basePath);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Upload
        // ──────────────────────────────────────────────────────────────────────

        public async Task<LoanContractAttachment> Upload(Guid loanContractId, IFormFile file, string? note)
        {
            // Validate file
            if (file == null || file.Length == 0)
                throw new ArgumentException("File không hợp lệ hoặc rỗng.");

            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Chỉ chấp nhận file PDF.");

            // Kiểm tra magic bytes: %PDF- (0x25 0x50 0x44 0x46 0x2D)
            // Đọc 5 byte đầu rồi reset stream về 0 để upload không bị thiếu dữ liệu
            using (var peek = file.OpenReadStream())
            {
                var header = new byte[5];
                var read   = await peek.ReadAsync(header, 0, 5);
                if (read < 5 || header[0] != 0x25 || header[1] != 0x50
                             || header[2] != 0x44 || header[3] != 0x46 || header[4] != 0x2D)
                    throw new ArgumentException("File không phải PDF hợp lệ (magic bytes không khớp).");
            }

            long maxBytes = Config.FileStorage?.MaxFileSizeBytes > 0
                ? Config.FileStorage.MaxFileSizeBytes
                : 20 * 1024 * 1024; // mặc định 20 MB

            if (file.Length > maxBytes)
                throw new ArgumentException($"File vượt quá kích thước tối đa cho phép ({maxBytes / 1024 / 1024} MB).");

            // Kiểm tra khoản vay tồn tại
            var loan = await DbContext.LoanContracts.FindAsync(loanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy khoản vay với Id = {loanContractId}");

            // Kiểm tra quyền theo chi nhánh
            if (!User.IsAdmin && loan.StoreId != User.StoreId)
                throw new UnauthorizedAccessException("Bạn không có quyền upload file cho khoản vay thuộc chi nhánh khác.");

            // Mỗi khoản vay chỉ có 1 file đính kèm
            bool exists = await DbContext.LoanContractAttachments
                .AnyAsync(a => a.LoanContractId == loanContractId);
            if (exists)
                throw new InvalidOperationException("Khoản vay này đã có hợp đồng đính kèm. Vui lòng liên hệ quản trị viên nếu cần thay thế.");

            // Tạo thư mục riêng theo loan_contract_id
            var contractDir = Path.Combine(_basePath, loanContractId.ToString());
            Directory.CreateDirectory(contractDir);

            // Tên file an toàn: không dùng tên file gốc (tránh path traversal)
            var safeFileName = $"{loanContractId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var absolutePath = Path.Combine(contractDir, safeFileName);
            var relativePath = Path.Combine(loanContractId.ToString(), safeFileName);

            // Ghi file ra đĩa
            await using (var fs = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(fs);
            }

            var attachment = new LoanContractAttachment
            {
                AttachmentId   = Guid.CreateVersion7(),
                LoanContractId = loanContractId,
                FileName       = file.FileName,       // tên file gốc để hiển thị
                FileSize       = file.Length,
                ContentType    = "application/pdf",
                StoragePath    = relativePath,         // đường dẫn tương đối
                UploadedBy     = CommonLib.GetGUID(User.UserId),
                Note           = note,
            };

            DbContext.LoanContractAttachments.Add(attachment);
            await DbContext.SaveChangesAsync();

            return attachment;
        }

        // ──────────────────────────────────────────────────────────────────────
        // GetByLoanContractId
        // ──────────────────────────────────────────────────────────────────────

        public async Task<LoanContractAttachment?> GetByLoanContractId(Guid loanContractId)
        {
            return await DbContext.LoanContractAttachments
                .FirstOrDefaultAsync(a => a.LoanContractId == loanContractId);
        }

        // ──────────────────────────────────────────────────────────────────────
        // GetFileForStream – mở FileStream để controller phục vụ viewer / download
        // ──────────────────────────────────────────────────────────────────────

        public async Task<(Stream stream, LoanContractAttachment meta)> GetFileForStream(Guid attachmentId)
        {
            var result = await DbContext.LoanContractAttachments
                .Where(a => a.AttachmentId == attachmentId)
                .Select(a => new { Meta = a, ContractStoreId = a.LoanContract.StoreId })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Không tìm thấy file đính kèm.");

            // Kiểm tra quyền truy cập theo chi nhánh
            if (!User.IsAdmin && result.ContractStoreId != User.StoreId)
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập file này.");

            var attachment = result.Meta;
            var fullPath = Path.Combine(_basePath, attachment.StoragePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File vật lý không tồn tại: {attachment.StoragePath}");

            // FileShare.Read cho phép nhiều request đọc đồng thời
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, attachment);
        }
    }
}
