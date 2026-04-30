using CrediFlow.API.Utils;
using CrediFlow.Common.Models;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Controllers
{
    /// <summary>Các tác vụ vận hành / migration dành riêng cho ADMIN.</summary>
    // [Authorize(Policy = "AdminOnly")]
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly CrediflowContext _db;
        private readonly ILogger<AdminController> _logger;

        public AdminController(CrediflowContext db, ILogger<AdminController> logger)
        {
            _db     = db;
            _logger = logger;
        }

        /// <summary>
        /// Batch-mã hóa toàn bộ CCCD plaintext còn lại trong bảng customers.
        /// Chỉ xử lý các dòng có national_id_hash IS NULL (chưa migrate).
        /// Chạy theo batch 100 bản ghi, trả về số lượng đã xử lý.
        /// </summary>
        // POST api/Admin/MigrateNationalIdEncryption
        [HttpPost]
        public async Task<ActionResult<ResultAPI>> MigrateNationalIdEncryption(
            [FromQuery] int batchSize = 1000, CancellationToken ct = default)
        {
            int processed = 0;
            int errors    = 0;

            try
            {
                while (true)
                {
                    // Lấy batch tiếp theo chưa migrate (hash còn null)
                    var batch = await _db.Customers
                        .Where(c => c.NationalIdHash == null)
                        .Take(batchSize)
                        .ToListAsync(ct);

                    if (batch.Count == 0) break;

                    foreach (var customer in batch)
                    {
                        try
                        {
                            var plain = customer.NationalId?.Trim() ?? string.Empty;
                            if (string.IsNullOrEmpty(plain))
                            {
                                // Không có CCCD — chỉ set hash rỗng để thoát vòng lặp
                                customer.NationalIdHash = "";
                                errors++;
                                continue;
                            }

                            // Nếu CCCD chưa được mã hóa (là plaintext), mã hóa và tính hash
                            // Nếu đã là ciphertext (decrypt thành công), chỉ cần tính hash
                            string normalizedPlain;
                            try
                            {
                                normalizedPlain = NationalIdEncryption.Decrypt(plain);
                                // Decrypt thành công → đã mã hóa → chỉ cần cập nhật hash
                            }
                            catch
                            {
                                // Decrypt thất bại → đây là plaintext → cần encrypt
                                normalizedPlain = plain.ToUpper();
                                customer.NationalId = NationalIdEncryption.Encrypt(normalizedPlain);
                            }

                            // Cập nhật customer_code (****XXXX) nếu vẫn là giá trị cũ
                            var masked = normalizedPlain.Length >= 4
                                ? "****" + normalizedPlain[^4..]
                                : normalizedPlain;
                            customer.CustomerCode = masked;

                            customer.NationalIdHash = NationalIdEncryption.ComputeSearchHash(normalizedPlain.ToUpper());
                            processed++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi khi migrate CCCD cho customer {Id}", customer.CustomerId);
                            customer.NationalIdHash = ""; // đánh dấu đã xử lý (dù lỗi) để thoát vòng lặp
                            errors++;
                        }
                    }

                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("CCCD migration: đã xử lý {Count} bản ghi (tổng: {Total}, lỗi: {Err})",
                        batch.Count, processed, errors);
                }

                return Ok(ResultAPI.Success(new { Processed = processed, Errors = errors },
                    $"Migration hoàn tất: {processed} bản ghi thành công, {errors} lỗi."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng khi chạy MigrateNationalIdEncryption");
                return Ok(ResultAPI.Error(new { Processed = processed, Errors = errors },
                    $"Lỗi: {ex.Message}", 500));
            }
        }

        /// <summary>Kiểm tra trạng thái migration: còn bao nhiêu bản ghi chưa migrate.</summary>
        // GET api/Admin/NationalIdMigrationStatus
        [HttpGet]
        public async Task<ActionResult<ResultAPI>> NationalIdMigrationStatus(CancellationToken ct = default)
        {
            var total      = await _db.Customers.CountAsync(ct);
            var notMigrated = await _db.Customers.CountAsync(c => c.NationalIdHash == null, ct);
            var migrated   = total - notMigrated;

            return Ok(ResultAPI.Success(new
            {
                Total       = total,
                Migrated    = migrated,
                NotMigrated = notMigrated,
                IsComplete  = notMigrated == 0
            }));
        }
    }
}
