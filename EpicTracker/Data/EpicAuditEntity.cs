namespace EpicTracker.Data;

public class EpicAuditEntity
{
    public int Id { get; set; }
    public string EpicId { get; set; } = default!;
    public string EpicAgentId { get; set; } = default!;
    public string FromState { get; set; } = default!;
    public string ToState { get; set; } = default!;
    public string? EpicAgentInstruction { get; set; }
    public DateTime Timestamp { get; set; }
    public string? HumanInLoop { get; set; }
    public string? AgentSwarm { get; set; }

    public EpicEntity Epic { get; set; } = default!;
}

