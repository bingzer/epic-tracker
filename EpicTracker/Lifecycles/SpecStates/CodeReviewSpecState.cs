namespace EpicTracker.Lifecycles.SpecStates;

internal class CodeReviewSpecState : SpecState
{
    public const string StateName = "code_review";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        if (spec.IsCodeReviewApproved is null)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Coordinate a direct code review between the coding agent and reviewer — do not relay messages.
                    1. Tell {spec.AssignedAgentName} to send their deliverables and spec context directly to {spec.ReviewerAgentName} via tmux-broker.
                    2. Tell {spec.ReviewerAgentName} to review the implementation at {spec.SpecDocPath} and send their verdict (APPROVED or REJECTED with notes) directly back to you via tmux-broker — not to the coding agent.
                    Wait for {spec.ReviewerAgentName}'s verdict, then call update_spec(IsCodeReviewApproved, true/false) and advance_spec({spec.Id}).
                    """);
        }

        if (spec.IsCodeReviewApproved == false)
        {
            spec.IsCodeDone = false;
            spec.IsCodeReviewApproved = null;

            return new CodingSpecState();
        }

        return new AcSpecState();
    }

    protected override bool UpdateSpecFieldAt(SpecContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Spec.IsCodeReviewApproved))
        {
            context.Spec.IsCodeReviewApproved = bool.Parse(value);
            return true;
        }

        return false;
    }
}
