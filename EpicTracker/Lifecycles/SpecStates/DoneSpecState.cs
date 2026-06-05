namespace EpicTracker.Lifecycles.SpecStates;

internal class DoneSpecState : SpecState
{
    public const string StateName = "done";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return Exit(
            context: context,
            instruction: "Spec is complete. No further action needed."
        );
    }
}
