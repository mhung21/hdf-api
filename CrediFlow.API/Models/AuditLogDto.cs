namespace CrediFlow.API.Models
{
    public class AuditLogDto
    {
        public Guid AuditLogId { get; set; }
        public string TableName { get; set; } = null!;
        public string ActionCode { get; set; } = null!;
        public string? OldData { get; set; }
        public string? NewData { get; set; }
        public DateTime ChangedAt { get; set; }
        public Guid? ChangedBy { get; set; }
        public string? ChangedByName { get; set; }
    }
}
