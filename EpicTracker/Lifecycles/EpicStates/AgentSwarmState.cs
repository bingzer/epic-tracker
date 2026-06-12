namespace EpicTracker.Lifecycles.EpicStates;

internal class AgentSwarmState : EpicState
{
    private const int MaxIterations = 5;

    public const string StateName = "agent_swarm";
    public override string Name => StateName;

    protected override bool UpdateEpicFieldAt(EpicContext context, string fieldName, string value) => true;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        var epic = context.Epic;

        if (epic.AgentSwarm is null)
        {
            throw new InvalidOperationException("AgentSwarm must be set on the epic before entering AgentSwarmState.");
        }

        var swarm = epic.AgentSwarm;

        if (epic.HumanInLoop is not null)
        {
            swarm.HumanInput = epic.HumanInLoop.HumanInput;
            epic.ResetHumanApproval();
        }

        if (swarm.HasConsensus)
        {
            return MoveTo(swarm.ToStateName);
        }

        if (!swarm.IsComplete && swarm.KickoffPosted)
        {
            var voted = swarm.Agreements
                .Where(a => a.HasAgreed.HasValue)
                .Select(a => $"{a.AgentId} ({(a.HasAgreed == true ? "AGREE" : "DISAGREE")}{(string.IsNullOrWhiteSpace(a.Note) ? "" : $" — \"{a.Note}\"")})");

            var pending = swarm.Agreements
                .Where(a => !a.HasAgreed.HasValue)
                .Select(a => a.AgentId);

            return Exit(
                context: context,
                instruction: $"""
                    Not all agents have voted yet.
                    Voted: {string.Join(", ", voted)}
                    Pending: {string.Join(", ", pending)}
                    Submit the remaining agreements via submit_agreement, then call advance("{epic.Id}").
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

        swarm.Iteration++;
        swarm.KickoffPosted = false;

        var channelId = $"epic-{epic.Id}";
        await context.Broker.CreateChannel(channelId, epic.EpicAgentName, cancellationToken);
        foreach (var agreement in swarm.Agreements)
        {
            await context.Broker.InviteToChannel(channelId, agreement.AgentId, epic.EpicAgentName, cancellationToken);
        }

        var kickoff = BuildKickoffMessage(
            objective: swarm.Objective,
            participants: swarm.Agreements.Select(a => a.AgentId).ToList(),
            epicAgentName: epic.EpicAgentName,
            channelId: channelId,
            iteration: swarm.Iteration,
            agentDomainFocus: swarm.AgentDomainFocus);

        await context.Broker.PostToChannel(channelId, epic.EpicAgentName, kickoff, cancellationToken);
        swarm.KickoffPosted = true;

        return Exit(
            context: context,
            instruction: BuildInstruction(epic.Id, epic.EpicAgentName, swarm)
        );
    }

    internal static string BuildKickoffMessage(
        string objective,
        IReadOnlyList<string> participants,
        string epicAgentName,
        string channelId,
        int iteration,
        IReadOnlyDictionary<string, string>? agentDomainFocus = null)
    {
        var participantList = string.Join(", ", participants);
        var discussionEnabled = participants.Count > 1;
        var discussionLine = discussionEnabled
            ? "yes — discuss peer-to-peer before voting"
            : "no — post your verdict directly";

        var domainFocusBlock = "";
        if (agentDomainFocus is { Count: > 0 })
        {
            var lines = string.Join("\n", agentDomainFocus.Select(kv => $"- {kv.Key}: {kv.Value}"));
            domainFocusBlock = $"\nDomain focus:\n{lines}\n";
        }

        return $"""
            Agent Swarm Iteration #{iteration}.

            Objective: {objective}

            Participants ({participants.Count}): {participantList}
            Coordinator: {epicAgentName} — direct scope/business questions here
            Channel: #{channelId}
            {domainFocusBlock}
            Discussion: {discussionLine}

            Reply format (post to this channel when ready):
            VOTE: AGREE | DISAGREE | BLOCKED
            REASON: <one paragraph>

            BLOCKED means: "I cannot vote because I need X from the coordinator before I can assess." It is not a soft DISAGREE.

            Do not wait for others before posting your verdict. After posting, leave the channel. The coordinator will collect all votes and advance the epic.
            """;
    }

    internal static string BuildCoordinatorInstruction(
        string epicId,
        IReadOnlyList<string> participants,
        string epicAgentName,
        string preamble,
        int iteration = 1,
        IReadOnlyDictionary<string, string>? agentDomainFocus = null,
        bool updateEpicDoc = false,
        string? footer = null)
    {
        var channelId = $"epic-{epicId}";
        var kickoff = BuildKickoffMessage(objective: preamble, participants: participants, epicAgentName: epicAgentName, channelId: channelId, iteration: iteration, agentDomainFocus: agentDomainFocus);

        var updateStep = updateEpicDoc
            ? "- Update the epic document to record each agent's conclusion and key insights\n               "
            : "";

        var footerLine = string.IsNullOrWhiteSpace(footer) ? "" : $"\n{footer}";

        return $"""
            Channel `{channelId}` has been created and all participants have been invited automatically.
            1. Post the following kickoff message to the channel via post_to_channel (from: {epicAgentName}):

            ---
            {kickoff}
            ---

            2. Step back and observe. Only intervene if an agent asks you a question or agents appear stuck.
            3. When all participants have posted VOTE: AGREE | DISAGREE | BLOCKED to the channel:
               {updateStep}- Call submit_agreement for each agent on their behalf
               - Call advance("{epicId}"){footerLine}
            """;
    }

    private static string BuildInstruction(string epicId, string epicAgentName, AgentSwarm swarm)
    {
        var channelId = $"epic-{epicId}";

        var reVoteNote = swarm.Iteration > 1
            ? $"This is re-vote round {swarm.Iteration}. At least one agent did not agree in the previous round. Summarize what was disputed when posting the kickoff."
            : "";

        var instruction = $"""
            Agent swarm coordinator instructions (iteration {swarm.Iteration} of {MaxIterations}):

            Channel `{channelId}` has been created, all participants have been invited, and the kickoff message has been posted automatically.

            {(reVoteNote.Length > 0 ? reVoteNote + "\n\n" : "")}1. Step back and observe. Only intervene if an agent asks you a question or agents appear stuck.

            2. When all participants have posted VOTE: AGREE | DISAGREE | BLOCKED to the channel:
               - Update the epic document: record each agent's conclusion and key insights, tick off resolved open questions
               - Call submit_agreement for each agent on their behalf
               - Call advance("{epicId}")

            3. If an agent does not respond, submit a disagreement with a note that they were unreachable.
            """;

        if (!string.IsNullOrWhiteSpace(swarm.HumanInput))
        {
            instruction += $"\n\nHuman feedback from previous round: {swarm.HumanInput}";
        }

        return instruction;
    }
}
