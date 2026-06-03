namespace EpicTracker.Data;

public class EpicEntity
{
    public string Id { get; set; } = default!;
    public string? Name { get; set; }
    public string EpicAgent { get; set; } = default!;
    public string? Brief { get; set; }
    public string Slug { get; set; } = default!;
    public bool NeedsMockup { get; set; }
    public bool IsDocDrafted { get; set; }
    public string? MockupPath { get; set; }
    public bool IsMockupDone { get; set; }
    public bool IsSpecListApproved { get; set; }
    public string? ReviewerAgentId { get; set; }
    public string CodingAgents { get; set; } = default!;
    public string CurrentStateName { get; set; } = default!;
    public string? HumanInLoop { get; set; }
    public string? AgentSwarm { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<SpecEntity> Specs { get; set; } = [];
    public ICollection<EpicAuditEntity> Audits { get; set; } = [];
}

