namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Terminal state. Spec is complete.
/// </summary>
internal class DoneSpecState : SpecState
{
    public override string Name => "done";

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return this;
    }
}
