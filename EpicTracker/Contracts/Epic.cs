namespace EpicTracker.Contracts;

public class Epic
{
    public string Id { get; set; } = default!;
    public string? Name { get; set; }
    public string EpicAgent { get; set; } = default!;
    public string? Brief { get; set; }
    public string Slug { get; set; } = default!;
    public string EpicDocumentPath => $"epics/{Slug}/epic.md";
    public string EpicGovernancePath => $"epics/{Slug}/governance.md";
    public bool NeedsMockup { get; set; }
    public bool IsDocDrafted { get; set; }
    public string? MockupPath { get; set; }
    public bool IsMockupDone { get; set; }
    public List<string> CodingAgents { get; set; } = [];
    public List<Spec> Specs { get; set; } = [];

    public string CurrentStateName { get; set; } = default!;
    public string? EpicAgentInstruction { get; private set; }
    public HumanInLoop? HumanInLoop { get; set; }
    public AgentSwarm? AgentSwarm { get; set; }

    public void SetEpicAgentInstruction(string instruction)
    {
        EpicAgentInstruction = instruction;
    }

}
