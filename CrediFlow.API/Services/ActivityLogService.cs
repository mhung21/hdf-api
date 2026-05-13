using System.Text.Json;
using System.Text.RegularExpressions;
using CrediFlow.API.Models;
using CrediFlow.Common.Services;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrediFlow.API.Services
{
    public interface IActivityLogService
    {
        Task LogAsync(ActivityLogWriteModel model);
        Task<object> SearchAsync(ActivityLogSearchRequest request);
    }

    public class ActivityLogService : IActivityLogService
    {
        private readonly CrediflowContext _context;
        private readonly IUserInfoService _user;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ActivityLogService> _logger;

        public ActivityLogService(
            CrediflowContext context,
            IUserInfoService user,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ActivityLogService> logger)
        {
            _context = context;
            _user = user;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(ActivityLogWriteModel model)
        {
            try
            {
                var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
                if (ip?.IsIPv4MappedToIPv6 == true) ip = ip.MapToIPv4();

                var sql = @"
INSERT INTO activity_logs
(activity_log_id, module_code, action_code, entity_type, entity_id, summary, old_data, new_data, metadata,
 customer_id, loan_contract_id, store_id, changed_by, changed_at_utc, ip_address, request_path)
VALUES
(@activity_log_id, @module_code, @action_code, @entity_type, @entity_id, @summary, CAST(@old_data AS jsonb), CAST(@new_data AS jsonb), CAST(@metadata AS jsonb),
 @customer_id, @loan_contract_id, @store_id, @changed_by, @changed_at_utc, CAST(@ip_address AS inet), @request_path);";

                await _context.Database.ExecuteSqlRawAsync(
                    sql,
                    new Npgsql.NpgsqlParameter("activity_log_id", Guid.CreateVersion7()),
                    new Npgsql.NpgsqlParameter("module_code", NormalizeCode(model.ModuleCode)),
                    new Npgsql.NpgsqlParameter("action_code", NormalizeCode(model.ActionCode)),
                    new Npgsql.NpgsqlParameter("entity_type", NormalizeCode(model.EntityType)),
                    new Npgsql.NpgsqlParameter("entity_id", (object?)model.EntityId ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("summary", (object?)Truncate(model.Summary, 500) ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("old_data", (object?)SanitizeJson(model.OldData) ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("new_data", (object?)SanitizeJson(model.NewData) ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("metadata", (object?)SanitizeJson(model.Metadata) ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("customer_id", (object?)model.CustomerId ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("loan_contract_id", (object?)model.LoanContractId ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("store_id", (object?)(model.StoreId ?? _user.StoreId) ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("changed_by", (object?)(model.ChangedBy ?? _user.UserId) ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("changed_at_utc", DateTime.UtcNow),
                    new Npgsql.NpgsqlParameter("ip_address", (object?)ip?.ToString() ?? DBNull.Value),
                    new Npgsql.NpgsqlParameter("request_path", (object?)Truncate(model.RequestPath ?? _httpContextAccessor.HttpContext?.Request.Path.Value, 500) ?? DBNull.Value)
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[ActivityLogService] Khong ghi duoc activity log: module={ModuleCode}, action={ActionCode}",
                    model.ModuleCode,
                    model.ActionCode);
            }
        }

        public async Task<object> SearchAsync(ActivityLogSearchRequest request)
        {
            var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
            var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 200);

            var conditions = new List<string>();
            var parameters = new List<Npgsql.NpgsqlParameter>();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                conditions.Add("(LOWER(COALESCE(al.summary, '')) LIKE @keyword OR LOWER(COALESCE(al.request_path, '')) LIKE @keyword OR LOWER(al.module_code) LIKE @keyword OR LOWER(al.action_code) LIKE @keyword)");
                parameters.Add(new Npgsql.NpgsqlParameter("keyword", $"%{request.Keyword.Trim().ToLower()}%"));
            }

            if (request.ModuleCodes != null && request.ModuleCodes.Count > 0)
            {
                var normalized = request.ModuleCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeCode).ToArray();
                conditions.Add("al.module_code = ANY(@module_codes)");
                parameters.Add(new Npgsql.NpgsqlParameter<string[]>("module_codes", normalized));
            }

            if (request.ActionCodes != null && request.ActionCodes.Count > 0)
            {
                var normalized = request.ActionCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeCode).ToArray();
                conditions.Add("al.action_code = ANY(@action_codes)");
                parameters.Add(new Npgsql.NpgsqlParameter<string[]>("action_codes", normalized));
            }

            if (request.ChangedBy.HasValue)
            {
                conditions.Add("al.changed_by = @changed_by");
                parameters.Add(new Npgsql.NpgsqlParameter("changed_by", request.ChangedBy.Value));
            }

            if (request.CustomerId.HasValue)
            {
                conditions.Add("al.customer_id = @customer_id");
                parameters.Add(new Npgsql.NpgsqlParameter("customer_id", request.CustomerId.Value));
            }

            if (request.LoanContractId.HasValue)
            {
                conditions.Add("al.loan_contract_id = @loan_contract_id");
                parameters.Add(new Npgsql.NpgsqlParameter("loan_contract_id", request.LoanContractId.Value));
            }

            if (request.FromUtc.HasValue)
            {
                conditions.Add("al.changed_at_utc >= @from_utc");
                parameters.Add(new Npgsql.NpgsqlParameter("from_utc", request.FromUtc.Value));
            }

            if (request.ToUtc.HasValue)
            {
                conditions.Add("al.changed_at_utc <= @to_utc");
                parameters.Add(new Npgsql.NpgsqlParameter("to_utc", request.ToUtc.Value));
            }

            var whereSql = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : string.Empty;
            var sortColumn = (request.SortBy?.Trim().ToLowerInvariant() ?? "changedatutc") switch
            {
                "modulecode" => "al.module_code",
                "actioncode" => "al.action_code",
                "changedby" => "al.changed_by",
                _ => "al.changed_at_utc",
            };
            var sortDirection = request.SortDesc ? "DESC" : "ASC";

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var countCmd = connection.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(1) FROM activity_logs al {whereSql};";
            foreach (var p in parameters)
                countCmd.Parameters.Add(CloneParameter(p));
            var totalCountObj = await countCmd.ExecuteScalarAsync();
            var totalCount = totalCountObj == null ? 0 : Convert.ToInt32(totalCountObj);

            var dataCmd = connection.CreateCommand();
            dataCmd.CommandText = $@"
SELECT
    al.activity_log_id,
    al.module_code,
    al.action_code,
    al.entity_type,
    al.entity_id,
    al.summary,
    al.old_data::text,
    al.new_data::text,
    al.metadata::text,
    al.customer_id,
    al.loan_contract_id,
    al.store_id,
    al.changed_by,
    COALESCE(NULLIF(au.full_name, ''), au.username) AS changed_by_name,
    al.changed_at_utc,
    al.request_path
FROM activity_logs al
LEFT JOIN app_users au ON au.user_id = al.changed_by
{whereSql}
ORDER BY {sortColumn} {sortDirection}
LIMIT @limit OFFSET @offset;";
            foreach (var p in parameters)
                dataCmd.Parameters.Add(CloneParameter(p));
            dataCmd.Parameters.Add(new Npgsql.NpgsqlParameter("offset", (pageIndex - 1) * pageSize));
            dataCmd.Parameters.Add(new Npgsql.NpgsqlParameter("limit", pageSize));

            var items = new List<ActivityLogItemDto>();
            await using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new ActivityLogItemDto
                {
                    ActivityLogId = reader.GetGuid(0),
                    ModuleCode = reader.GetString(1),
                    ActionCode = reader.GetString(2),
                    EntityType = reader.GetString(3),
                    EntityId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                    Summary = reader.IsDBNull(5) ? null : reader.GetString(5),
                    OldData = reader.IsDBNull(6) ? null : reader.GetString(6),
                    NewData = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Metadata = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CustomerId = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                    LoanContractId = reader.IsDBNull(10) ? null : reader.GetGuid(10),
                    StoreId = reader.IsDBNull(11) ? null : reader.GetGuid(11),
                    ChangedBy = reader.IsDBNull(12) ? null : reader.GetGuid(12),
                    ChangedByName = reader.IsDBNull(13) ? null : reader.GetString(13),
                    ChangedAtUtc = reader.GetDateTime(14),
                    RequestPath = reader.IsDBNull(15) ? null : reader.GetString(15),
                });
            }

            return new
            {
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Items = items,
            };
        }

        private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static string? SanitizeJson(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var sanitized = Regex.Replace(
                value,
                @"(?i)(\""(password|currentPassword|newPassword|token|accessToken|refreshToken|nationalId)\""\s*:\s*\"")[^\""]*(\"")",
                "$1***$3");

            if (sanitized.Length > 4096)
                sanitized = sanitized[..4096] + "...(truncated)";

            try
            {
                using var doc = JsonDocument.Parse(sanitized);
                return sanitized;
            }
            catch
            {
                return JsonSerializer.Serialize(sanitized);
            }
        }

        private static Npgsql.NpgsqlParameter CloneParameter(Npgsql.NpgsqlParameter source)
        {
            var copy = new Npgsql.NpgsqlParameter(source.ParameterName, source.Value ?? DBNull.Value);
            if (source.NpgsqlDbType != default)
                copy.NpgsqlDbType = source.NpgsqlDbType;
            return copy;
        }
    }
}