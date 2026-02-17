using System;

namespace MenuProUI.Models;

public class AuditEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string Details { get; set; } = "";
}
