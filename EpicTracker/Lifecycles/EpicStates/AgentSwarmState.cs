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
            epic.HumanInLoop = new HumanInLoop
            {
                Questions = "Agents could not reach consensus after maximum iterations. Please review and provide direction.",
                ApproveToStateName = this.Name,
                RejectToStateName = this.Name
            };

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

            Instruct each agent to review the objective and submit their agreement or disagreement with notes via the AgreementTable.
            """;

        if (!string.IsNullOrWhiteSpace(swarm.HumanInput))
        {
            instruction += $"\n\nHuman feedback from previous round: {swarm.HumanInput}";
        }

        return instruction;
    }
}

