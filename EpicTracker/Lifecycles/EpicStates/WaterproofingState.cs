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

        if (epic.AgentSwarmHasConsensus())
        {
            epic.ResetAgentSwarm();
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
                instruction: AgentSwarmState.BuildCoordinatorInstruction(
                    epicId: epic.Id,
                    allParticipants: allParticipants,
                    preamble: $"Agent swarm raised for waterproofing (iteration {epic.WaterproofingIterations} of {context.Options.MaxWaterproofingIterations}).",
                    updateEpicDoc: true
                )
            );
        }
        
        return new SpecWritingState();
    }
}

