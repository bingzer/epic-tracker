namespace EpicTracker.Lifecycles.EpicStates;

internal class AgentSwarmState : EpicState
{
    private const int MaxIterations = 5;

    public override string Name => "agent_swarm";

    public override async Task<EpicState> MoveNext(Epic epic, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (epic.AgentSwarm is null)
        {
            throw new InvalidOperationException("AgentSwarm must be set on the epic before entering AgentSwarmState.");
        }

        var swarm = epic.AgentSwarm;

        if (swarm.HasConsensus)
        {
            return EpicState.Create(swarm.ToStateName);
        }

        if (swarm.Iteration >= MaxIterations)
        {
            epic.RaiseHumanInLoop(
                questions: "Agents could not reach consensus after maximum iterations. Please review and provide direction.",
                approveToStateName: Name,
                rejectToStateName: Name,
                instruction: "Max swarm iterations reached. Raised HumanInLoop for human input."
            );

            return new HumanInLoopState();
        }

        if (epic.HumanInLoop?.HumanInput is not null)
        {
            swarm.HumanInput = epic.HumanInLoop.HumanInput;
        }

        swarm.Iteration++;

        epic.SetEpicAgentInstruction(BuildInstruction(swarm));

        return this;
    }

    private static string BuildInstruction(AgentSwarm swarm)
    {
        var agentList = string.Join(", ", swarm.Agreements.Select(a => a.AgentId));

        var instruction = $"""
            You are coordinating an agent swarm. This is iteration {swarm.Iteration} of {MaxIterations}.

            Objective: {swarm.Objective}

            Agents: {agentList}

            For each agent, send them a tmux-broker message asking them to review the objective and reply with either AGREE or DISAGREE and a brief note explaining their scope or concern.
            Coding agents do NOT have MCP access — you are their proxy. Once you have collected a response from each agent, call submit_agreement for each one on their behalf, then call advance.
            Do not ask the user. Do not wait indefinitely — if an agent does not respond, submit a disagreement with a note that they were unreachable.
            """;

        if (!string.IsNullOrWhiteSpace(swarm.HumanInput))
        {
            instruction += $"\n\nHuman feedback from previous round: {swarm.HumanInput}";
        }

        return instruction;
    }
}

