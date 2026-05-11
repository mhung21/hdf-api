using CrediFlow.API.Models;
using CrediFlow.API.Utils;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace CrediFlow.API.Services
{
    public interface ICustomerService : IBaseService<Customer>
    {
        /// <summary>Tạo mới hoặc cập nhật khách hàng tùy theo <see cref="CUCustomerModel.CustomerId"/>.</summary>
        Task<Customer> Save(CUCustomerModel model);

        /// <summary>Lấy danh sách khách hàng theo quyền của user hiện tại.</summary>
        Task<IList<Customer>> GetAlls();

        /// <summary>Tìm kiếm khách hàng theo từ khóa với phân trang, sắp xếp và các bộ lọc bổ sung.</summary>
        Task<object> SearchCustomer(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc,
            bool? hasBadDebt = null, bool? hasActiveLoan = null, List<Guid>? filterStoreIds = null);

        /// <summary>Tra cứu khách hàng theo số căn cước công dân (CCCD). Trả về null nếu chưa từng vay.</summary>
        Task<Customer?> GetByNationalId(string nationalId);

        /// <summary>Chuyển giao khách hàng cho nhân viên khác phụ trách (chỉ Manager/Admin).</summary>
        Task AssignCustomer(Guid customerId, Guid targetUserId);
    }

    public class CustomerService : BaseService<Customer, CrediflowContext>, ICustomerService
    {
        public CustomerService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user)
        {
        }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        // ──────────────────────────────────────────────────────────────────────
        // GetAlls
        // ──────────────────────────────────────────────────────────────────────

        public async Task<IList<Customer>> GetAlls()
        {
            var query = DbContext.Customers.AsQueryable();

            // Admin thấy tất cả; các role khác chỉ thấy KH thuộc chi nhánh được phân quyền
            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(c => c.FirstStoreId.HasValue && storeScopeIds.Contains(c.FirstStoreId.Value));

            var list = await query
                .OrderBy(c => c.FullName)
                .ToListAsync();

            // Giải mã CCCD sau khi đọc từ DB
            foreach (var c in list)
                c.NationalId = NationalIdEncryption.Decrypt(c.NationalId);

            return list;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Save (Create / Update)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<Customer> Save(CUCustomerModel model)
        {
            // Kiểm tra giá trị FirstSourceType hợp lệ
            if (model.FirstSourceType != null &&
                model.FirstSourceType != SourceType.Ctv &&
                model.FirstSourceType != SourceType.VangLai &&
                model.FirstSourceType != SourceType.KhachCu)
            {
                throw new ArgumentException(
                    $"Nguồn tiếp cận không hợp lệ: '{model.FirstSourceType}'. Chỉ chấp nhận '{SourceType.Ctv}', '{SourceType.VangLai}' hoặc '{SourceType.KhachCu}'.");
            }

            bool isCreate = model.CustomerId == null || model.CustomerId == Guid.Empty;
            Customer obj;

            if (isCreate)
            {
                // ── Tạo mới ──────────────────────────────────────────────────
                var creatorId = CommonLib.GetGUID(User.UserId);
                obj = new Customer
                {
                    CustomerId = Guid.CreateVersion7(),
                    CreatedBy = creatorId,
                    AssignedToUserId = creatorId,   // mặc định phụ trách = người tạo
                };
                DbContext.Customers.Add(obj);
            }
            else
            {
                // ── Cập nhật ───────────────────────────────────────────
                obj = await DbContext.Customers.FindAsync(model.CustomerId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy khách hàng với Id = {model.CustomerId}");

                // Staff chỉ được sửa khách hàng mình tạo hoặc được gán phụ trách,
                // trừ khi được cấp quyền CUSTOMER_UPDATE qua vai trò bổ sung
                if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager &&
                    !await PermissionChecker.HasPermissionAsync(DbContext, CachingHelper, User.UserId, User.RoleCode, "CUSTOMER_UPDATE"))
                {
                    var staffId = CommonLib.GetGUID(User.UserId);
                    if (obj.CreatedBy != staffId && obj.AssignedToUserId != staffId)
                        throw new UnauthorizedAccessException(
                            "Bạn chỉ có thể chỉnh sửa khách hàng mà bạn đang phụ trách.");
                }
            }

            // Ánh xạ các trường từ model → entity
            obj.FirstSourceType = model.FirstSourceType ?? obj.FirstSourceType;
            obj.FirstStoreId = model.FirstStoreId ?? obj.FirstStoreId;
            obj.ReferredByCollaboratorId = model.ReferredByCollaboratorId ?? obj.ReferredByCollaboratorId;

            // Mã hóa CCCD và tính hash để lưu DB
            var nationalIdPlain = model.NationalId?.Trim() ?? string.Empty;
            obj.NationalId    = NationalIdEncryption.Encrypt(nationalIdPlain);
            obj.NationalIdHash = NationalIdEncryption.ComputeSearchHash(nationalIdPlain);

            // Mã khách hàng sinh tự động theo quy tắc:
            // YY + {chữ cái đầu tên chi nhánh bỏ dấu} + '-' + {số tăng dần 5 chữ số theo năm}
            // Ví dụ: 26PHN3-00001
            if (isCreate)
            {
                if (!obj.FirstStoreId.HasValue)
                    throw new InvalidOperationException("Thiếu thông tin chi nhánh tiếp nhận để tạo mã khách hàng.");

                obj.CustomerCode = await GenerateCustomerCodeAsync(obj.FirstStoreId.Value);
            }

            obj.FullName = model.FullName;
            obj.DateOfBirth = model.DateOfBirth ?? obj.DateOfBirth;
            obj.Gender = model.Gender ?? obj.Gender;
            obj.Phone = model.Phone ?? obj.Phone;
            obj.Address = model.Address ?? obj.Address;
            obj.HasBadHistory = model.HasBadHistory;
            obj.BadHistoryNote = model.BadHistoryNote ?? obj.BadHistoryNote;

            await DbContext.SaveChangesAsync();

            return obj;
        }

        // ──────────────────────────────────────────────────────────────────────
        // SearchCustomer
        // ──────────────────────────────────────────────────────────────────────

        public async Task<object> SearchCustomer(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc,
            bool? hasBadDebt = null, bool? hasActiveLoan = null, List<Guid>? filterStoreIds = null)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = pageSize < 1 ? 1 : pageSize;

            var query = DbContext.Customers.AsQueryable();

            // Admin thấy tất cả; các role khác chỉ thấy KH thuộc chi nhánh được phân quyền
            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(c => c.FirstStoreId.HasValue && storeScopeIds.Contains(c.FirstStoreId.Value));

            if (filterStoreIds != null && filterStoreIds.Count > 0)
                query = query.Where(c => c.FirstStoreId.HasValue && filterStoreIds.Contains(c.FirstStoreId.Value));

            // ── Bộ lọc nợ xấu ──────────────────────────────────────────────
            if (hasBadDebt == true)
                query = query.Where(c =>
                    c.HasBadHistory ||
                    c.LoanContracts.Any(l => l.StatusCode == LoanContractStatus.BadDebt) ||
                    DbContext.BadDebtCases.Any(b => b.LoanContract.CustomerId == c.CustomerId));
            else if (hasBadDebt == false)
                query = query.Where(c =>
                    !c.HasBadHistory &&
                    !c.LoanContracts.Any(l => l.StatusCode == LoanContractStatus.BadDebt) &&
                    !DbContext.BadDebtCases.Any(b => b.LoanContract.CustomerId == c.CustomerId));

            // ── Bộ lọc đang có hợp đồng vay hoạt động ─────────────────────
            // Hợp đồng đang hoạt động = chưa kết thúc (không phải CANCELLED/SETTLED/CLOSED/BAD_DEBT_CLOSED)
            if (hasActiveLoan == true)
                query = query.Where(c => c.LoanContracts.Any(l =>
                    l.StatusCode != LoanContractStatus.Cancelled &&
                    l.StatusCode != LoanContractStatus.Settled &&
                    l.StatusCode != LoanContractStatus.Closed &&
                    l.StatusCode != LoanContractStatus.BadDebtClosed));
            else if (hasActiveLoan == false)
                query = query.Where(c => !c.LoanContracts.Any(l =>
                    l.StatusCode != LoanContractStatus.Cancelled &&
                    l.StatusCode != LoanContractStatus.Settled &&
                    l.StatusCode != LoanContractStatus.Closed &&
                    l.StatusCode != LoanContractStatus.BadDebtClosed));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                // CCCD đã mã hóa — tìm kiếm bằng hash chính xác; tên/SĐT vẫn dùng LIKE
                var nationalIdHash = NationalIdEncryption.ComputeSearchHash(keyword.ToUpper());
                query = query.Where(c =>
                    c.FullName.ToLower().Contains(keyword) ||
                    c.NationalIdHash == nationalIdHash ||
                    (c.CustomerCode != null && c.CustomerCode.ToLower().Contains(keyword)) ||
                    (c.Phone != null && c.Phone.Contains(keyword)));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "createdat") switch
            {
                "nationalid"   => sortDesc ? query.OrderByDescending(c => c.NationalIdHash) : query.OrderBy(c => c.NationalIdHash),
                "customercode" => sortDesc ? query.OrderByDescending(c => c.CustomerCode) : query.OrderBy(c => c.CustomerCode),
                "phone"        => sortDesc ? query.OrderByDescending(c => c.Phone)        : query.OrderBy(c => c.Phone),
                "updatedat"    => sortDesc ? query.OrderByDescending(c => c.UpdatedAt)    : query.OrderBy(c => c.UpdatedAt),
                "fullname"     => sortDesc ? query.OrderByDescending(c => c.FullName)     : query.OrderBy(c => c.FullName),
                _              => sortDesc ? query.OrderBy(c => c.CreatedAt)              : query.OrderByDescending(c => c.CreatedAt),  // default: newest first
            };

            var rawItems = await sorted
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.CustomerId,
                    c.CustomerCode,
                    NationalIdEnc = c.NationalId, // cipher text — giải mã bên dưới
                    c.FullName,
                    c.Phone,
                    c.Address,
                    c.FirstSourceType,
                    HasBadHistory = c.HasBadHistory
                        || c.LoanContracts.Any(l => l.StatusCode == LoanContractStatus.BadDebt)
                        || DbContext.BadDebtCases.Any(b =>
                            b.LoanContract.CustomerId == c.CustomerId),
                    c.BadHistoryNote,
                    c.FirstStoreId,
                    StoreName = c.FirstStore != null ? c.FirstStore.StoreName : null,
                    c.DateOfBirth,
                    c.Gender,
                    c.ReferredByCollaboratorId,
                    CollaboratorName = c.ReferredByCollaborator != null ? c.ReferredByCollaborator.FullName : null,
                    c.CreatedAt,
                    CreatedByName = c.CreatedBy.HasValue
                        ? DbContext.AppUsers.Where(u => u.UserId == c.CreatedBy.Value).Select(u => u.FullName).FirstOrDefault()
                        : null,
                })
                .ToListAsync();

            // Giải mã CCCD sau khi đọc từ DB
            var items = rawItems.Select(c => new
            {
                c.CustomerId,
                c.CustomerCode,
                NationalId = NationalIdEncryption.Decrypt(c.NationalIdEnc),
                c.FullName,
                c.Phone,
                c.Address,
                c.FirstSourceType,
                c.HasBadHistory,
                c.BadHistoryNote,
                c.FirstStoreId,
                c.StoreName,
                c.DateOfBirth,
                c.Gender,
                c.ReferredByCollaboratorId,
                c.CollaboratorName,
                c.CreatedAt,
                c.CreatedByName,
            }).ToList();

            return new
            {
                TotalCount = total,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Items = items
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // GetByNationalId
        // ──────────────────────────────────────────────────────────────────────

        public async Task<Customer?> GetByNationalId(string nationalId)
        {
            if (string.IsNullOrWhiteSpace(nationalId))
                return null;

            // Tìm kiếm bằng hash (CCCD đã mã hóa trong DB)
            var hash = NationalIdEncryption.ComputeSearchHash(nationalId.Trim().ToUpper());
            var customer = await DbContext.Customers
                .FirstOrDefaultAsync(c => c.NationalIdHash == hash);

            if (customer != null)
                customer.NationalId = NationalIdEncryption.Decrypt(customer.NationalId);

            return customer;
        }

        // ──────────────────────────────────────────────────────────────────────
        // AssignCustomer – chuyển giao khách hàng cho nhân viên khác
        // ──────────────────────────────────────────────────────────────────────

        public async Task AssignCustomer(Guid customerId, Guid targetUserId)
        {
            if (!User.IsAdmin && !User.IsStoreManager && !User.IsRegionalManager)
                throw new UnauthorizedAccessException(
                    "Chỉ quản lý cửa hàng hoặc admin mới có quyền chuyển giao khách hàng.");

            var obj = await DbContext.Customers
                      .FirstOrDefaultAsync(c => c.CustomerId == customerId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy khách hàng với Id = {customerId}");

            // Lấy tên chi nhánh cũ cho audit log
            string? oldStoreName = obj.FirstStoreId.HasValue
                ? await DbContext.Stores
                    .Where(s => s.StoreId == obj.FirstStoreId.Value)
                    .Select(s => s.StoreName)
                    .FirstOrDefaultAsync()
                : null;

            // StoreManager chỉ chuyển giao trong phạm vi cửa hàng mình
            if (!User.IsAdmin)
            {
                var storeScopeIds = GetStoreScopeIds();
                if (storeScopeIds is not null && (!obj.FirstStoreId.HasValue || !storeScopeIds.Contains(obj.FirstStoreId.Value)))
                    throw new UnauthorizedAccessException(
                        "Bạn không có quyền chuyển giao khách hàng của chi nhánh khác.");
            }

            // Lấy thông tin nhân viên mới để xác định chi nhánh đích
            var targetUser = await DbContext.AppUsers
                .Include(u => u.Store)
                .FirstOrDefaultAsync(u => u.UserId == targetUserId)
                ?? throw new KeyNotFoundException($"Không tìm thấy nhân viên với Id = {targetUserId}");

            if (!targetUser.StoreId.HasValue)
                throw new InvalidOperationException(
                    $"Nhân viên '{targetUser.FullName}' chưa được gán chi nhánh, không thể chuyển giao.");

            // Snapshot dữ liệu cũ để ghi log
            var oldAssignedUserId = obj.AssignedToUserId;
            var oldStoreId = obj.FirstStoreId;

            // Cập nhật người phụ trách + chi nhánh của khách hàng
            obj.AssignedToUserId = targetUserId;
            obj.FirstStoreId = targetUser.StoreId.Value;
            obj.UpdatedAt = DateTime.Now;

            // Chuyển giao toàn bộ hợp đồng vay của khách hàng sang nhân viên mới + chi nhánh mới
            var contracts = await DbContext.LoanContracts
                .Where(c => c.CustomerId == customerId)
                .ToListAsync();

            foreach (var contract in contracts)
            {
                contract.AssignedToUserId = targetUserId;
                contract.StoreId = targetUser.StoreId.Value;
                contract.UpdatedAt = DateTime.Now;
            }

            // Ghi audit log chuyển giao
            DbContext.AuditLogs.Add(new AuditLog
            {
                AuditLogId = Guid.CreateVersion7(),
                TableName = "customers",
                RecordId = customerId,
                ActionCode = "ASSIGN",
                OldData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    AssignedToUserId = oldAssignedUserId,
                    FirstStoreId = oldStoreId,
                    StoreName = oldStoreName,
                }),
                NewData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    AssignedToUserId = targetUserId,
                    FirstStoreId = targetUser.StoreId,
                    StoreName = targetUser.Store?.StoreName,
                    ContractsTransferred = contracts.Count,
                }),
                ChangedBy = CommonLib.GetGUID(User.UserId),
                ChangedAt = DateTime.Now,
                CustomerId = customerId,
                StoreId = targetUser.StoreId,
                Note = $"Chuyển giao khách hàng '{obj.FullName}' và {contracts.Count} hợp đồng " +
                       $"từ CN '{oldStoreName}' sang CN '{targetUser.Store?.StoreName}' " +
                       $"cho NV '{targetUser.FullName}'",
            });

            await DbContext.SaveChangesAsync();
        }

        private async Task<string> GenerateCustomerCodeAsync(Guid storeId)
        {
            var storeName = await DbContext.Stores
                .Where(s => s.StoreId == storeId)
                .Select(s => s.StoreName)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(storeName))
                throw new KeyNotFoundException($"Không tìm thấy chi nhánh với Id = {storeId}");

            var year2 = (DateTime.Now.Year % 100).ToString("00");
            var storeToken = BuildStoreToken(storeName);
            var prefix = $"{year2}{storeToken}";
            var codePrefix = $"{prefix}-";

            var usedCodes = await DbContext.Customers
                .Where(c => c.CustomerCode != null && c.CustomerCode.StartsWith(codePrefix))
                .Select(c => c.CustomerCode!)
                .ToListAsync();

            var maxSeq = 0;
            foreach (var code in usedCodes)
            {
                var dash = code.IndexOf('-');
                if (dash < 0 || dash + 1 >= code.Length) continue;
                var seqText = code[(dash + 1)..];
                if (seqText.Length != 5 || !int.TryParse(seqText, out var seq)) continue;
                if (seq > maxSeq) maxSeq = seq;
            }

            return $"{prefix}-{(maxSeq + 1):00000}";
        }

        private static string BuildStoreToken(string storeName)
        {
            var normalized = storeName
                .Normalize(NormalizationForm.FormD)
                .Replace('đ', 'd')
                .Replace('Đ', 'D');

            var ascii = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) ascii.Append(char.ToUpperInvariant(ch));
                else ascii.Append(' ');
            }

            var token = string.Concat(
                ascii.ToString()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part[0])
            );

            return string.IsNullOrWhiteSpace(token) ? "CN" : token;
        }
    }
}
