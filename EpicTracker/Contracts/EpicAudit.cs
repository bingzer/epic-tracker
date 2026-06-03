namespace EpicTracker.Contracts;

public class EpicAudit
{
    public int Id { get; set; }
    public string EpicId { get; set; } = default!;
    public string EpicAgentId { get; set; } = default!;
    public string FromState { get; set; } = default!;
    public string ToState { get; set; } = default!;
    public string? EpicAgentInstruction { get; set; }
    public DateTime Timestamp { get; set; }
}
