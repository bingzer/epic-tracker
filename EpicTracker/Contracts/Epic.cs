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
    public string? ReviewerAgentId { get; set; }
    public List<string> CodingAgents { get; set; } = [];
    public List<Spec> Specs { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public string CurrentStateName { get; set; } = default!;
    public string? EpicAgentInstruction { get; private set; }
    public HumanInLoop? HumanInLoop { get; set; }
    public AgentSwarm? AgentSwarm { get; set; }

    public void SetEpicAgentInstruction(string instruction)
    {
        EpicAgentInstruction = instruction;
    }

    /// <summary>Returns true when the active agent swarm has reached full consensus (all agents agreed).</summary>
    public bool AgentSwarmHasConsensus() => AgentSwarm?.HasConsensus == true;

    /// <summary>
    /// Raises a new agent swarm. Participants are all <see cref="CodingAgents"/> plus the <see cref="EpicAgent"/>.
    /// After calling this, set <see cref="EpicAgentInstruction"/> to tell the epic agent to message each participant via tmux,
    /// collect AGREE/DISAGREE responses, call <c>submit_agreement</c> for each, then call <c>advance</c>.
    /// </summary>
    public void RaiseAgentSwarm(string objective, string toStateName)
    {
        AgentSwarm = new AgentSwarm
        {
            Objective = objective,
            ToStateName = toStateName,
            Agreements = CodingAgents
                .Select(id => new AgentAgreement { AgentId = id })
                .Append(new AgentAgreement { AgentId = EpicAgent })
                .ToList()
        };
    }

    /// <summary>Clears the active agent swarm.</summary>
    public void ResetAgentSwarm() => AgentSwarm = null;

    /// <summary>Returns the human's approval decision, or <c>null</c> if no response has been received yet.</summary>
    public bool? HasHumanApproved() => HumanInLoop?.IsApproved;

    public bool IsAwaitingAgentSwarmResponse() => AgentSwarm is not null && !AgentSwarm.HasConsensus;

    public bool IsAwaitingHumanResponse() => HumanInLoop is not null && HumanInLoop.IsApproved is null;

    public bool IsAwaitingAgentSwarmOrHumanResponse() => IsAwaitingAgentSwarmResponse() || IsAwaitingHumanResponse();

    /// <summary>Returns true when a <see cref="HumanInLoop"/> has been raised and is waiting for a human response.</summary>
    public bool IsAwaitingHumanApproval() => HumanInLoop is not null;

    /// <summary>Clears the active <see cref="HumanInLoop"/>. Call this after handling the human's response.</summary>
    public void ResetHumanApproval() => HumanInLoop = null;

    /// <summary>
    /// Raises a <see cref="HumanInLoop"/>, blocking the epic until a human approves or rejects via the dashboard.
    /// Approval routes to <paramref name="approveToStateName"/>; rejection routes to <paramref name="rejectToStateName"/>.
    /// After calling this, return <c>new HumanInLoopState()</c> from <c>MoveNext</c>.
    /// </summary>
    public void RaiseHumanInLoop(string questions, string approveToStateName, string rejectToStateName)
    {
        HumanInLoop = new HumanInLoop
        {
            Questions = questions,
            ApproveToStateName = approveToStateName,
            RejectToStateName = rejectToStateName
        };
    }

}
