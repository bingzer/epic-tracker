namespace EpicTracker.Data;

public class SpecEntity
{
    public string Id { get; set; } = default!;
    public string EpicId { get; set; } = default!;
    public string AssignedAgentName { get; set; } = default!;
    public string? ReviewerAgentName { get; set; }
    public bool? IsACRequired { get; set; }
    public bool? IsCodeReviewRequired { get; set; }
    public string? SpecDocPath { get; set; }
    public bool IsSpecApproved { get; set; }
    public bool IsAbandoned { get; set; }
    public bool IsSpecDrafted { get; set; }
    public bool? IsAcPassed { get; set; }
    public bool IsReadyToCode { get; set; }
    public bool IsCodeDone { get; set; }
    public bool? IsCodeReviewApproved { get; set; }
    public int CodeReviewIterations { get; set; }
    public string CurrentStateName { get; set; } = default!;
    public string? EpicAgentInstruction { get; set; }
    public string? HumanInLoop { get; set; }
    public string? AgentSwarm { get; set; }
    public string? ScopeChange { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public EpicEntity Epic { get; set; } = default!;
}
