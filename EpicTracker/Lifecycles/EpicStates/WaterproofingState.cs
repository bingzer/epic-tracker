namespace EpicTracker.Lifecycles.EpicStates;

internal class WaterproofingState : EpicState
{
    public const string StateName = "waterproofing";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;
        epic.LastKnownStateName = Name;

        if (epic.NeedsMockup && !epic.IsMockupDone)
        {
            return new MockupState();
        }

        if (epic.NeedsAgentSwarm())
        {
            epic.WaterproofingIterations++;

            if (epic.WaterproofingIterations > context.Options.MaxWaterproofingIterations)
            {
                return RaiseHumanInLoop(
                    context: context,
                    questions: $"""
                        Waterproofing has exceeded {context.Options.MaxWaterproofingIterations} iterations without reaching consensus.
                        Review the epic document at {epic.EpicDocumentPath} and decide whether to approve it as-is or reject and restart waterproofing.
                        """,
                    approveToStateName: SpecWritingState.StateName,
                    rejectToStateName: StateName,
                    instruction: $"""
                        Waterproofing iteration cap reached ({context.Options.MaxWaterproofingIterations} rounds).
                        Human-in-loop raised. Call advance("{epic.Id}") and wait for the human to decide.
                        """
                );
            }

            var allParticipants = string.Join(", ", epic.CodingAgentNames.Append(epic.EpicAgentName));

            return RaiseAgentSwarm(
                context: context,
                objective: $"""
                    Read the epic document at {epic.EpicDocumentPath}.
                    Your role: contribute your domain knowledge and technical constraints relevant to this epic.
                    Share what you know that affects feasibility, scope, or approach — things the epic agent cannot know without your expertise.
                    DISAGREE if the epic does not yet reflect your input or contains something technically incorrect.
                    AGREE only when the epic accurately represents the technical reality as you understand it (LGTM).
                    Do NOT begin any implementation.
                    """,
                whenApprovedStateName: Name,
                instruction: $"""
                    Agent swarm raised for waterproofing (iteration {epic.WaterproofingIterations} of {context.Options.MaxWaterproofingIterations}).

                    1. Create channel `swarm-epic-{epic.Id}` via create_channel, then invite all participants: {allParticipants}.
                    2. Post the kickoff (per governance.md swarm protocol) to the channel via post_to_channel.
                    3. Step back and observe. Only intervene if an agent asks you a question or agents appear stuck.
                    4. When all participants have posted their assessment to the channel:
                       - Update the epic document to record each agent's conclusion and key insights
                       - Call submit_agreement for each agent on their behalf
                       - Leave channel `swarm-epic-{epic.Id}` via leave_channel (you are the last to leave — this deletes the channel)
                       - Call advance("{epic.Id}")
                    """
            );
        }
        
        return new SpecWritingState();
    }
}

