namespace EpicTracker.Lifecycles.EpicStates;

/// <summary>
/// Terminal state. Epic is complete and closed.
/// </summary>
internal class ClosedState : EpicState
{
    public const string StateName = "closed";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        if (!context.FileSystem.FileExists(context.Epic.EpicDeliverablesPath))
        {
            return Exit(
                context: context,
                instruction: $"Cannot close this epic: deliverables.md is missing. " +
                             $"Create {context.Epic.EpicDeliverablesPath} documenting what was built and delivered, then call advance again."
            );
        }

        context.Epic.LastKnownStateName = Name;

        await context.Broker.DeleteChannel($"epic-{context.Epic.Id}", context.Epic.EpicAgentName, cancellationToken);

        return Exit(
            context: context,
            instruction: "This epic is already closed."
        );
    }
}
