namespace EpicTracker.Lifecycles.SpecStates;

internal class ReadySpecState : SpecState
{
    public const string StateName = "ready";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        if (!spec.IsReadyToCode)
        {
            return Exit(
                context: context,
                instruction: $"Spec {spec.Id} is waiting for a human to click Code Now in the dashboard before coding begins."
            );
        }
        
        return new CodingSpecState();
    }
}
