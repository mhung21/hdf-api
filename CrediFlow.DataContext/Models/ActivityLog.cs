#nullable enable
using System;
using System.Net;

namespace CrediFlow.DataContext.Models;

public partial class ActivityLog
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

    public DateTime ChangedAtUtc { get; set; }

    public IPAddress? IpAddress { get; set; }

    public string? RequestPath { get; set; }
}