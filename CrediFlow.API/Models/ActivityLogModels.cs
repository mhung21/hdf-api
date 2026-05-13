namespace CrediFlow.API.Models
{
    public class ActivityLogSearchRequest
    {
        public string? Keyword { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "changedAtUtc";
        public bool SortDesc { get; set; } = true;
        public List<string>? ModuleCodes { get; set; }
        public List<string>? ActionCodes { get; set; }
        public Guid? ChangedBy { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? LoanContractId { get; set; }
    }

    public class ActivityLogItemDto
    {
        public Guid ActivityLogId { get; set; }
        public string ModuleCode { get; set; } = null!;
        public string ActionCode { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public Guid? EntityId { get; set; }
        public string? Summary { get; set; }
        public string? OldData { get; set; }
        public string? NewData { get; set; }
        public string? Metadata { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? LoanContractId { get; set; }
        public Guid? StoreId { get; set; }
        public Guid? ChangedBy { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string? RequestPath { get; set; }
    }

    public class ActivityLogWriteModel
    {
        public string ModuleCode { get; set; } = null!;
        public string ActionCode { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public Guid? EntityId { get; set; }
        public string? Summary { get; set; }
        public string? OldData { get; set; }
        public string? NewData { get; set; }
        public string? Metadata { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? LoanContractId { get; set; }
        public Guid? StoreId { get; set; }
        public Guid? ChangedBy { get; set; }
        public string? RequestPath { get; set; }
    }
}