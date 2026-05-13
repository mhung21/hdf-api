using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrediFlow.DataContext.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrediFlow.API.Interceptors;

/// <summary>
/// EF Core interceptor ghi nhật ký thay đổi dữ liệu (INSERT/UPDATE/DELETE)
/// vào bảng audit_logs tự động khi SaveChanges được gọi.
/// Được đăng ký là singleton thông qua DI và thêm vào DbContextOptions.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditInterceptor> _logger;

    // Các bảng không cần audit (tránh vòng lặp vô tận hoặc log noise)
    private static readonly HashSet<string> SkipTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "audit_logs",
        "activity_logs",
        "contract_audit_logs",
        "data_access_logs",
        "user_login_histories",
        "user_sessions",
        "loan_status_histories",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor, ILogger<AuditInterceptor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        try { AddAuditEntries(eventData.Context); }
        catch (Exception ex) { _logger.LogError(ex, "[AuditInterceptor] Lỗi khi tạo audit log (SavingChanges), bỏ qua để không chặn nghiệp vụ."); }
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        try { AddAuditEntries(eventData.Context); }
        catch (Exception ex) { _logger.LogError(ex, "[AuditInterceptor] Lỗi khi tạo audit log (SavingChangesAsync), bỏ qua để không chặn nghiệp vụ."); }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Core logic
    // ──────────────────────────────────────────────────────────────────────────

    private void AddAuditEntries(DbContext? context)
    {
        if (context == null) return;

        var userId = GetCurrentUserId();

        // Snapshot TRƯỚC khi thêm AuditLog vào context để tránh capture chính nó
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        var auditLogs = new List<AuditLog>(entries.Count);

        foreach (var entry in entries)
        {
            var tableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
            if (SkipTables.Contains(tableName)) continue;

            var recordId = GetPrimaryKeyGuid(entry);
            if (recordId == Guid.Empty) continue;

            var actionCode = entry.State switch
            {
                EntityState.Added    => "INSERT",
                EntityState.Modified => "UPDATE",
                // EntityState.Deleted: domain này không xóa bản ghi (immutable records).
                // DB CHECK constraint cho phép ('INSERT','UPDATE','STATUS_CHANGE','ASSIGN')
                // nên bỏ qua Deleted để tránh vi phạm constraint.
                _                    => null,
            };
            if (actionCode == null) continue;

            string? oldData = null;
            string? newData = null;

            if (entry.State == EntityState.Modified)
            {
                oldData = SerializeValues(entry.OriginalValues);
                newData = SerializeValues(entry.CurrentValues);
            }
            else // Added
            {
                newData = SerializeValues(entry.CurrentValues);
            }

            auditLogs.Add(new AuditLog
            {
                AuditLogId     = Guid.CreateVersion7(),
                TableName      = tableName,
                RecordId       = recordId,
                ActionCode     = actionCode,
                OldData        = oldData,
                NewData        = newData,
                ChangedBy      = userId,
                ChangedAt      = DateTime.Now,
                StoreId        = TryGetProperty<Guid?>(entry, "StoreId"),
                LoanContractId = TryGetProperty<Guid?>(entry, "LoanContractId"),
                CustomerId     = TryGetProperty<Guid?>(entry, "CustomerId"),
            });
        }

        if (auditLogs.Count > 0)
            context.Set<AuditLog>().AddRange(auditLogs);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return null;

        var sub = user.FindFirst("sub")?.Value
               ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static Guid GetPrimaryKeyGuid(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk == null || pk.Properties.Count != 1) return Guid.Empty;

        var propName = pk.Properties[0].Name;
        var values   = entry.State == EntityState.Deleted ? entry.OriginalValues : entry.CurrentValues;
        return values[propName] is Guid g ? g : Guid.Empty;
    }

    private static string? SerializeValues(Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues values)
    {
        var dict = new Dictionary<string, object?>(values.Properties.Count);
        foreach (var prop in values.Properties)
        {
            if (prop.ClrType == typeof(byte[])) continue; // bỏ binary
            var val = values[prop.Name];
            // IPAddress không serialize được mặc định → chuyển sang string
            dict[prop.Name] = val is System.Net.IPAddress ip ? ip.ToString() : val;
        }
        return dict.Count > 0 ? JsonSerializer.Serialize(dict, JsonOptions) : null;
    }

    private static T? TryGetProperty<T>(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string propertyName)
    {
        var prop = entry.Metadata.FindProperty(propertyName);
        if (prop == null) return default;
        var values = entry.State == EntityState.Deleted ? entry.OriginalValues : entry.CurrentValues;
        try { return (T?)values[propertyName]; }
        catch { return default; }
    }
}
