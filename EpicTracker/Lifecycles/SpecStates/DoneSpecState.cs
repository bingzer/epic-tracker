using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Terminal state. Spec is complete.
/// </summary>
internal class DoneSpecState : SpecState
{
    public override string Name => "done";

    protected override async Task<SpecState> Next(Spec spec, ILogger logger, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return this;
    }
}
