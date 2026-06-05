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
            var reviewInstruction = context.IsCodeReviewRequired
                ? $"""
                    When implementation is complete:
                    1. Send to you (epic agent): SPEC {spec.Id} STATUS: coding-done
                    2. Send to {spec.ReviewerAgentName} via tmux-broker: their assignment with the spec doc at {spec.SpecDocPath} and all relevant context.
                    Wait for {spec.ReviewerAgentName} to reply — do not relay anything between them and the reviewer.
                    """
                : $"""
                    When implementation is complete, send to you (epic agent): SPEC {spec.Id} STATUS: coding-done
                    """;

            return Exit(
                context: context,
                instruction: $"""
                    Hand off spec {spec.Id} to {spec.AssignedAgentName} via tmux-broker. Tell them to implement the spec at {spec.SpecDocPath}.
                    If this is a retry after a rejected code review, include the rejection reason from {spec.ReviewerAgentName}'s last message in your assignment to the coding agent.
                    {reviewInstruction}
                    When you receive SPEC {spec.Id} STATUS: coding-done, call update_spec({spec.Id}, IsCodeDone, true). That automatically advances the spec.
                    """
            );
        }

        if (context.IsCodeReviewRequired)
        {
            return new CodeReviewSpecState();
        }

        if (context.IsACRequired)
        {
            return new AcSpecState();
        }

        return new DoneSpecState();
    }

    protected override bool UpdateSpecFieldAt(SpecContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Spec.IsCodeDone))
        {
            context.Spec.IsCodeDone = bool.Parse(value);
            return true;
        }

        return false;
    }
}
