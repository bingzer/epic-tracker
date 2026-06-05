namespace EpicTracker.Contracts;

public class Spec
{
    public string Id { get; set; } = default!;
    public string EpicId { get; set; } = default!;
    public string AssignedAgentId { get; set; } = default!;
    public string? ReviewerAgentName { get; set; }
    public bool CodeReviewRequired { get; set; }
    public string? SpecDocPath { get; set; }
    public bool IsSpecApproved { get; set; }
    public bool IsAbandoned { get; set; }
    public bool IsSpecDrafted { get; set; }
    public bool? IsAcPassed { get; set; }
    public bool IsReadyToCode { get; set; }
    public bool IsCodeDone { get; set; }
    public bool? IsCodeReviewApproved { get; set; }

    public string CurrentStateName { get; set; } = default!;
    public string? EpicAgentInstruction { get; private set; }
    public HumanInLoop? HumanInLoop { get; set; }
    public AgentSwarm? AgentSwarm { get; set; }

    public void SetEpicAgentInstruction(string instruction)
    {
        EpicAgentInstruction = instruction;
    }

}
