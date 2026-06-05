namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Terminal state. Spec is complete.
/// </summary>
internal class DoneSpecState : SpecState
{
    public const string StateName = "done";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return this;
    }
}
