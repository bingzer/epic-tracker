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
        await Task.CompletedTask;

        return Exit(
            context: context,
            instruction: "This epic is already closed."
        );
    }
}
