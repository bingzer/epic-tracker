namespace EpicTracker.Contracts;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = default!;
    public string EpicState { get; set; } = default!;
    public string? SpecState { get; set; }
    public string EpicId { get; set; } = default!;
    public string? SpecId { get; set; }
    public string? Actor { get; set; }
    public string? Message { get; set; }
}
