using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CrediFlow.API.Services
{
    public interface IAppUserService : IBaseService<AppUser>
    {
        Task<AppUser> Save(CUAppUserModel model);
        Task<IList<AppUser>> GetAlls();
        Task<AppUser?> GetDetailAsync(Guid id);
        Task<object> SearchAppUser(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc);
    }

    public class AppUserService : BaseService<AppUser, CrediflowContext>, IAppUserService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppUserService(
            CrediflowContext dbContext,
            ICachingHelper cachingHelper,
            IUserInfoService user,
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
            : base(dbContext, cachingHelper, user)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IList<AppUser>> GetAlls()
        {
            var query = DbContext.AppUsers.AsQueryable();

            if (!User.IsAdmin)
            {
                if (User.IsRegionalManager)
                {
                    var assignedStoreIds = User.AssignedStoreIds;
                    query = query.Where(u => u.StoreId != null && assignedStoreIds.Contains(u.StoreId.Value));
                }
                else
                {
                    query = query.Where(u => u.StoreId == User.StoreId);
                }
            }

            var items = await query
                .Include(u => u.UserStoreAssignments)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return items.Select(MapAppUser).ToList();
        }

        public async Task<AppUser> Save(CUAppUserModel model)
        {
            bool isCreate = model.UserId == null || model.UserId == Guid.Empty;

            if (isCreate)
            {
                if (string.IsNullOrWhiteSpace(model.Password))
                    throw new ArgumentException("Mat khau la bat buoc khi tao moi nguoi dung.");

                if (!Enum.TryParse<Consts.UserRoleCode>(model.RoleCode?.Trim(), true, out var roleCodeEnum))
                    throw new InvalidOperationException("RoleCode khong hop le. Gia tri hop le: ADMIN | REGIONAL_MANAGER | STORE_MANAGER | STAFF.");

                string? email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
                string? phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

                using var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(Config.Urls.IdentityServer);

                var payload = new
                {
                    username = model.Username,
                    fullName = model.FullName,
                    password = model.Password,
                    email,
                    phone,
                    roleCode = roleCodeEnum,
                    storeId = model.StoreId,
                    storeIds = model.StoreIds,
                    isActive = model.IsActive,
                    mustChangePassword = model.MustChangePassword,
                };

                var incomingToken = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(incomingToken))
                    client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(incomingToken);

                var response = await client.PostAsJsonAsync("/api/auth/register", payload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Dang ky tai khoan that bai: {errorBody}");
                }

                var created = await DbContext.AppUsers
                    .FirstOrDefaultAsync(u => u.Username == model.Username)
                    ?? throw new InvalidOperationException("Dang ky thanh cong nhung khong tim thay tai khoan. Vui long thu lai.");

                if (roleCodeEnum == Consts.UserRoleCode.REGIONAL_MANAGER)
                {
                    await SaveRegionalManagerStores(created.UserId, model.StoreIds ?? []);
                }

                var createdRefreshed = await GetDetailAsync(created.UserId);
                if (createdRefreshed == null)
                    throw new InvalidOperationException("Khong tai duoc thong tin nguoi dung vua tao.");

                return createdRefreshed;
            }

            var obj = await DbContext.AppUsers.FindAsync(model.UserId)
                      ?? throw new KeyNotFoundException($"Khong tim thay nguoi dung voi Id = {model.UserId}");

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                obj.PasswordHash = HashPassword(model.Password);
                obj.PasswordChangedAt = DateTime.Now;
            }

            obj.Username = model.Username;
            obj.FullName = model.FullName;
            obj.Phone = model.Phone ?? obj.Phone;
            obj.Email = model.Email ?? obj.Email;
            obj.RoleCode = model.RoleCode;
            obj.StoreId = model.RoleCode == RoleCode.RegionalManager || model.RoleCode == RoleCode.Admin ? null : model.StoreId;
            obj.IsActive = model.IsActive;
            obj.MustChangePassword = model.MustChangePassword;

            await DbContext.SaveChangesAsync();

            if (model.RoleCode == RoleCode.RegionalManager)
            {
                await SaveRegionalManagerStores(obj.UserId, model.StoreIds ?? []);
            }
            else
            {
                var oldAssignments = await DbContext.UserStoreAssignments
                    .Where(a => a.UserId == obj.UserId)
                    .ToListAsync();

                if (oldAssignments.Count > 0)
                {
                    DbContext.UserStoreAssignments.RemoveRange(oldAssignments);
                    await DbContext.SaveChangesAsync();
                }
            }

            var updatedRefreshed = await GetDetailAsync(obj.UserId);
            if (updatedRefreshed == null)
                throw new InvalidOperationException("Khong tai duoc thong tin nguoi dung vua cap nhat.");

            return updatedRefreshed;
        }

        public async Task<AppUser?> GetDetailAsync(Guid id)
        {
            var user = await DbContext.AppUsers
                .Include(u => u.UserStoreAssignments)
                .FirstOrDefaultAsync(u => u.UserId == id);

            return user == null ? null : MapAppUser(user);
        }

        private async Task SaveRegionalManagerStores(Guid userId, List<Guid> storeIds)
        {
            var existing = await DbContext.UserStoreAssignments
                .Where(a => a.UserId == userId)
                .ToListAsync();

            DbContext.UserStoreAssignments.RemoveRange(existing);

            var distinctStoreIds = storeIds
                .Where(sid => sid != Guid.Empty)
                .Distinct()
                .ToList();

            var newAssignments = distinctStoreIds.Select(sid => new UserStoreAssignment
            {
                AssignmentId = Guid.CreateVersion7(),
                UserId = userId,
                StoreId = sid,
                AssignedAt = DateTime.Now,
            }).ToList();

            await DbContext.UserStoreAssignments.AddRangeAsync(newAssignments);
            await DbContext.SaveChangesAsync();
        }

        public async Task<object> SearchAppUser(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = DbContext.AppUsers.AsQueryable();

            if (!User.IsAdmin)
            {
                if (User.IsRegionalManager)
                {
                    var assignedStoreIds = User.AssignedStoreIds;
                    query = query.Where(u => u.StoreId != null && assignedStoreIds.Contains(u.StoreId.Value));
                }
                else
                {
                    query = query.Where(u => u.StoreId == User.StoreId);
                }
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(keyword) ||
                    u.Username.ToLower().Contains(keyword) ||
                    (u.Email != null && u.Email.ToLower().Contains(keyword)) ||
                    (u.Phone != null && u.Phone.Contains(keyword)));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "fullname") switch
            {
                "username" => sortDesc ? query.OrderByDescending(u => u.Username) : query.OrderBy(u => u.Username),
                "rolecode" => sortDesc ? query.OrderByDescending(u => u.RoleCode) : query.OrderBy(u => u.RoleCode),
                "isactive" => sortDesc ? query.OrderByDescending(u => u.IsActive) : query.OrderBy(u => u.IsActive),
                "createdat" => sortDesc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
                _ => sortDesc ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName)
            };

            var items = await sorted
                .Include(u => u.UserStoreAssignments)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new
            {
                Total = total,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Items = items.Select(MapAppUser).ToList()
            };
        }

        private static AppUser MapAppUser(AppUser user)
        {
            return new AppUser
            {
                UserId = user.UserId,
                Username = user.Username,
                PasswordHash = string.Empty,
                FullName = user.FullName,
                Phone = user.Phone,
                Email = user.Email,
                RoleCode = user.RoleCode,
                StoreId = user.StoreId,
                StoreIds = user.UserStoreAssignments?.Select(a => a.StoreId).Distinct().ToList(),
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                PasswordChangedAt = user.PasswordChangedAt,
                MustChangePassword = user.MustChangePassword,
                FailedLoginAttempts = user.FailedLoginAttempts,
                LockedUntil = user.LockedUntil,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
            };
        }

        private static string HashPassword(string password)
        {
            // Chuẩn duy nhất của hệ thống: BCrypt cost 11
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
        }
    }
}
