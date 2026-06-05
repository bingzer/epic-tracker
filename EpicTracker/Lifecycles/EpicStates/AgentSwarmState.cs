namespace EpicTracker.Lifecycles.EpicStates;

internal class AgentSwarmState : EpicState
{
    private const int MaxIterations = 5;

    public const string StateName = "agent_swarm";
    public override string Name => StateName;

    protected override bool UpdateEpicFieldAt(EpicContext context, string fieldName, string value) => true;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (epic.AgentSwarm is null)
        {
            throw new InvalidOperationException("AgentSwarm must be set on the epic before entering AgentSwarmState.");
        }

        var swarm = epic.AgentSwarm;

        if (swarm.HasConsensus)
        {
            return MoveTo(swarm.ToStateName);
        }

        if (!swarm.IsComplete)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Not all agents have voted yet. Submit the remaining agreements via submit_agreement, then call advance("{epic.Id}").
                    """);
        }

        if (swarm.Iteration >= MaxIterations)
        {
            return RaiseHumanInLoop(
                context: context,
                questions: "Agents could not reach consensus after maximum iterations. Please review and provide direction.",
                approveToStateName: Name,
                rejectToStateName: Name,
                instruction: $"""
                    Max swarm iterations reached. HumanInLoop raised for human input.
                    Call advance("{epic.Id}") then wait for tmux to wake you.
                    """
            );
        }

        if (epic.HumanInLoop?.HumanInput is not null)
        {
            swarm.HumanInput = epic.HumanInLoop.HumanInput;
            epic.ResetHumanApproval();
        }

        swarm.Iteration++;

        return Exit(
            context: context,
            instruction: BuildInstruction(epic.Id, swarm)
        );
    }

    private static string BuildInstruction(string epicId, AgentSwarm swarm)
    {
        var agentList = string.Join(", ", swarm.Agreements.Select(a => a.AgentId));

        var reVoteNote = swarm.Iteration > 1
            ? $"This is re-vote round {swarm.Iteration}. At least one agent disagreed in the previous round. Tell them this is a follow-up vote, summarise what was disputed, and ask them to reconsider with that context."
            : "This is the first vote.";

        var instruction = $"""
            You are coordinating an agent swarm. This is iteration {swarm.Iteration} of {MaxIterations}.

            {reVoteNote}

            Objective: {swarm.Objective}

            Agents: {agentList}

            Once you have collected a response from each agent, call submit_agreement for each one on their behalf, then call advance("{epicId}").
            Do not ask the user. Do not wait indefinitely — if an agent does not respond, submit a disagreement with a note that they were unreachable.
            """;

        if (!string.IsNullOrWhiteSpace(swarm.HumanInput))
        {
            instruction += $"\n\nHuman feedback from previous round: {swarm.HumanInput}";
        }

        return instruction;
    }
}
