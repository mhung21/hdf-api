#nullable enable
using System;
using System.Collections.Generic;

namespace CrediFlow.DataContext.Models;

public partial class ContractAuditLog
{
    public Guid AuditLogId { get; set; }

    public Guid LoanContractId { get; set; }

    public string ActionCode { get; set; } = null!;

    public string? OldData { get; set; }

    public string? NewData { get; set; }

    public Guid? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Note { get; set; }

    public virtual AppUser? ChangedByNavigation { get; set; }

    public virtual LoanContract LoanContract { get; set; } = null!;
}
