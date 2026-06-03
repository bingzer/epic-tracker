namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Terminal state. Spec is complete.
/// </summary>
internal class DoneSpecState : SpecState
{
    public override string Name => "done";

    public override async Task<SpecState> MoveNext(Spec spec, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return this;
    }
}
