namespace EpicTracker.Lifecycles.EpicStates;

internal class WaterproofingState : EpicState
{
    public const string StateName = "waterproofing";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (epic.NeedsMockup && !epic.IsMockupDone)
        {
            return new MockupState();
        }

        if (epic.NeedsAgentSwarm())
        {
            return RaiseAgentSwarm(
                context: context,
                objective: $"""
                    Read the epic document at {epic.EpicDocumentPath} and reach agreement on scope and responsibilities.
                    Do NOT begin any implementation — this is scope alignment only.
                    """,
                whenApprovedStateName: Name,
                instruction: $"""
                    Agent swarm raised for waterproofing alignment.
                    Follow the governance document at {epic.EpicGovernancePath} for swarm instructions.
                    Call advance("{epic.Id}") after submitting all agreements.
                    """
            );
        }
        
        return new SpecWritingState();
    }
}

