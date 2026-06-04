namespace EpicTracker.Lifecycles.EpicStates;

/// <summary>
/// Terminal state. Epic is complete and closed.
/// </summary>
internal class ClosedState : EpicState
{
    public override string Name => "closed";

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        context.Epic.SetEpicAgentInstruction("This epic is already closed.");

        return this;
    }
}
