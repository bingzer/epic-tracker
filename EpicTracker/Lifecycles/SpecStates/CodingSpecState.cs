namespace EpicTracker.Lifecycles.SpecStates;

internal class CodingSpecState : SpecState
{
    public const string StateName = "coding";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        if (!spec.IsCodeDone)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Hand off spec {spec.Id} to {spec.AssignedAgentName} via tmux-broker. Tell them to implement the spec at {spec.SpecDocPath} and reply to you via tmux-broker when done.
                    Wait for their reply, then call update_spec({spec.Id}, IsCodeDone, true) and advance_spec({spec.Id}).
                    """
            );
        }

        if (spec.CodeReviewRequired)
        {
            return new CodeReviewSpecState();
        }

        return new AcSpecState();
    }
}
