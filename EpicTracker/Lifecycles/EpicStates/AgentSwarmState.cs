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
            instruction: BuildInstruction(epic.Id, epic.EpicAgentName, swarm)
        );
    }

    private static string BuildInstruction(string epicId, string epicAgentName, AgentSwarm swarm)
    {
        var channelId = $"swarm-epic-{epicId}";
        var agents = swarm.Agreements.Select(a => a.AgentId).ToList();
        var agentList = string.Join(", ", agents);
        var isSingleAgent = agents.Count == 1;

        var discussRule = isSingleAgent
            ? ""
            : $"- Discuss with the other participants in channel `{channelId}`\n";

        var processStep = isSingleAgent
            ? "- You are the only participant. Post your assessment directly to the channel"
            : $"- Discuss in channel `{channelId}` until you have formed your assessment";

        var kickoff = $"""
            You are participating in an agent swarm.

            Objective: {swarm.Objective}

            Participants: {agentList}
            Coordinator: {epicAgentName}
            Channel: {channelId}

            Rules:
            {discussRule}- Stay focused on your domain knowledge and technical constraints
            - Message the coordinator if you have questions about scope, business context, or anything outside your domain — the coordinator can escalate to a human if needed
            - You do not need to reach a definitive conclusion — share what you know, what you can commit to, and what concerns or uncertainties remain

            Process:
            {processStep}
            - When ready, post your assessment to the channel: AGREE, DISAGREE, or BLOCKED — with your reasoning
            - Leave the channel after posting your assessment
            """;

        var reVoteNote = swarm.Iteration > 1
            ? $"This is re-vote round {swarm.Iteration}. At least one agent did not agree in the previous round. Summarize what was disputed when posting the kickoff."
            : "";

        var instruction = $"""
            Agent swarm coordinator instructions (iteration {swarm.Iteration} of {MaxIterations}):

            {(reVoteNote.Length > 0 ? reVoteNote + "\n\n" : "")}1. Post the following kickoff message to channel `{channelId}` via post_to_channel:

            ---
            {kickoff}
            ---

            2. Step back and observe. Only intervene if an agent asks you a question or agents appear stuck.

            3. When all participants have posted their assessment to the channel:
               - Update the epic document to record each agent's conclusion and key insights
               - Call submit_agreement for each agent on their behalf
               - Leave channel `{channelId}` via leave_channel (you are the last to leave — this deletes the channel)
               - Call advance("{epicId}")

            4. If an agent does not respond, submit a disagreement with a note that they were unreachable.
            """;

        if (!string.IsNullOrWhiteSpace(swarm.HumanInput))
        {
            instruction += $"\n\nHuman feedback from previous round: {swarm.HumanInput}";
        }

        return instruction;
    }
}
