using System.Text.Json;
using CrediFlow.Common.Services;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CrediFlow.API.Services
{
    public interface IDataAccessLogService
    {
        /// <summary>
        /// Ghi nhật ký truy cập dữ liệu (xem, tải file, xuất báo cáo...).
        /// Fire-and-forget safe — không throw, không chặn nghiệp vụ chính.
        /// </summary>
        Task LogAsync(string resourceType, Guid? resourceId, string action,
                      string? queryParams = null, string? note = null);
    }

    public class DataAccessLogService : IDataAccessLogService
    {
        private readonly CrediflowContext          _context;
        private readonly IUserInfoService           _user;
        private readonly IHttpContextAccessor       _httpContextAccessor;
        private readonly ILogger<DataAccessLogService> _logger;

        public DataAccessLogService(CrediflowContext context, IUserInfoService user,
                                    IHttpContextAccessor httpContextAccessor,
                                    ILogger<DataAccessLogService> logger)
        {
            _context             = context;
            _user                = user;
            _httpContextAccessor = httpContextAccessor;
            _logger              = logger;
        }

        public async Task LogAsync(string resourceType, Guid? resourceId, string action,
                                   string? queryParams = null, string? note = null)
        {
            try
            {
                var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
                if (ip?.IsIPv4MappedToIPv6 == true) ip = ip.MapToIPv4();

                _context.DataAccessLogs.Add(new DataAccessLog
                {
                    AccessLogId    = Guid.CreateVersion7(),
                    UserId         = _user.UserId,
                    StoreId        = _user.StoreId,
                    ResourceType   = resourceType,
                    ResourceId     = resourceId,
                    Action         = action,
                    AccessedAt     = DateTime.Now,
                    IpAddress      = ip,
                    QueryParams    = EnsureJson(queryParams),
                    ResponseStatus = 200,
                    Note           = note,
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Lỗi audit không được chặn nghiệp vụ chính, nhưng phải log để theo dõi
                _logger.LogWarning(ex,
                    "[DataAccessLogService] Không ghi được data_access_log: resource={ResourceType}/{Action}",
                    resourceType, action);
            }
        }

        /// <summary>
        /// Đảm bảo giá trị là JSON hợp lệ để lưu vào cột jsonb.
        /// Nếu đã là JSON (object/array/string literal) thì giữ nguyên;
        /// nếu là plain string (query string, v.v.) thì serialize thành JSON string.
        /// </summary>
        private static string? EnsureJson(string? value)
        {
            if (value is null) return null;
            try
            {
                using var doc = JsonDocument.Parse(value);
                return value; // đã là JSON hợp lệ
            }
            catch
            {
                return JsonSerializer.Serialize(value); // wrap thành "\"...\""
            }
        }
    }
}
