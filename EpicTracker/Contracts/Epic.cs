namespace EpicTracker.Contracts;

public class Epic
{
    public string Id { get; set; } = default!;
    public string? Name { get; set; }
    public string EpicAgent { get; set; } = default!;
    public string? Brief { get; set; }
    public string Slug { get; set; } = default!;
    public string BasePath { get; set; } = default!;
    public string EpicDocumentPath => Path.Combine(BasePath, "epics", Slug, "epic.md");
    public string EpicGovernancePath => Path.Combine(BasePath, "epics", Slug, "governance.md");
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
    /// Sets <see cref="EpicAgentInstruction"/> to <paramref name="instruction"/> to tell the epic agent to message each
    /// participant via tmux, collect AGREE/DISAGREE responses, call <c>submit_agreement</c> for each, then call <c>advance</c>.
    /// </summary>
    public void RaiseAgentSwarm(string objective, string toStateName, string instruction)
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

        SetEpicAgentInstruction(instruction);
    }

    /// <summary>Clears the active agent swarm and sets <see cref="EpicAgentInstruction"/> to <paramref name="instruction"/>.</summary>
    public void ResetAgentSwarm(string instruction)
    {
        AgentSwarm = null;
        SetEpicAgentInstruction(instruction);
    }

    /// <summary>Returns true when no agent swarm has been raised yet.</summary>
    public bool NeedsAgentSwarm() => AgentSwarm is null;

    /// <summary>Returns true when no <see cref="HumanInLoop"/> has been raised yet — human review still needs to be requested.</summary>
    public bool NeedsHumanReview() => HumanInLoop is null;

    /// <summary>Returns true when a human has explicitly rejected via <see cref="HumanInLoop"/>.</summary>
    public bool IsHumanRejected() => HumanInLoop?.IsApproved == false;

    /// <summary>Returns true when a human has explicitly approved via <see cref="HumanInLoop"/>.</summary>
    public bool IsHumanApproved() => HumanInLoop?.IsApproved == true;

    /// <summary>Clears the active <see cref="HumanInLoop"/> and sets <see cref="EpicAgentInstruction"/> to <paramref name="instruction"/>.</summary>
    public void ResetHumanApproval(string instruction)
    {
        HumanInLoop = null;
        SetEpicAgentInstruction(instruction);
    }

    /// <summary>
    /// Raises a <see cref="HumanInLoop"/>, blocking the epic until a human approves or rejects via the dashboard.
    /// Approval routes to <paramref name="approveToStateName"/>; rejection routes to <paramref name="rejectToStateName"/>.
    /// Sets <see cref="EpicAgentInstruction"/> to <paramref name="instruction"/>.
    /// After calling this, return <c>new HumanInLoopState()</c> from <c>MoveNext</c>.
    /// </summary>
    public void RaiseHumanInLoop(string questions, string approveToStateName, string rejectToStateName, string instruction)
    {
        HumanInLoop = new HumanInLoop
        {
            Questions = questions,
            ApproveToStateName = approveToStateName,
            RejectToStateName = rejectToStateName
        };

        SetEpicAgentInstruction(instruction);
    }

}
