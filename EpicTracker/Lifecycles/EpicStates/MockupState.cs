namespace EpicTracker.Lifecycles.EpicStates;

internal class MockupState : EpicState
{
    public const string StateName = "mockup";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;
        epic.LastKnownStateName = Name;

        if (!epic.NeedsMockup)
        {
            return new WaterproofingState();
        }

        if (!epic.IsMockupDone)
        {
            return Exit(
                context: context, 
                instruction: $"""
                    Create mockup files at {epic.MockupDirectory}. Mockups can be HTML or plain text documents.
                    Once complete, call update_epic with field IsMockupDone=true then call advance("{epic.Id}").
                    """
            );
        }

        if (epic.NeedsAgentSwarm())
        {
            var participants = epic.CodingAgentNames.Append(epic.EpicAgentName).ToList();
            var objective = $"""
                Review the mockup files at {epic.MockupDirectory}.
                DISAGREE if the mockup does not meet requirements, has design gaps, or needs revision.
                AGREE only when the mockup accurately represents what should be built.
                Do NOT begin any implementation.
                """;

            return RaiseAgentSwarm(
                context: context,
                objective: objective,
                whenApprovedStateName: Name,
                instruction: AgentSwarmState.BuildCoordinatorInstruction(
                    epicId: epic.Id,
                    participants: participants,
                    epicAgentName: epic.EpicAgentName,
                    preamble: objective
                )
            );
        }

        if (!epic.AgentSwarmHasConsensus())
        {
            return new AgentSwarmState();
        }

        epic.ResetAgentSwarm();

        return new WaterproofingState();

    }

    protected override bool UpdateEpicFieldAt(EpicContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Epic.IsMockupDone))
        {
            context.Epic.IsMockupDone = bool.Parse(value);
            return true;
        }

        return false;
    }
}
