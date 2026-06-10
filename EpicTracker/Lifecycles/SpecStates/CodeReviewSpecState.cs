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
                    Step 1 — Tell {spec.AssignedAgentName}: Go to {spec.ReviewerAgentName} directly. Send them the spec doc at {spec.SpecDocPath} and all files they need to review. Do not route through PM.

                    Step 2 — {spec.ReviewerAgentName} will do the review.

                    Step 3 — Wait for {spec.ReviewerAgentName} to send their verdict to you AND {spec.AssignedAgentName}. Their message will be in this format:
                      SPEC {spec.Id} STATUS: reviewing          — reviewer has started (acknowledgement only)
                      SPEC {spec.Id} STATUS: review-approved    — call update_spec({spec.Id}, IsCodeReviewApproved, true)  [auto-advances]
                      SPEC {spec.Id} STATUS: review-rejected REASON: <reason>
                                                                — call update_spec({spec.Id}, IsCodeReviewApproved, false) [auto-advances]
                    Do not relay anything between {spec.AssignedAgentName} and {spec.ReviewerAgentName}.
                    If rejected, the state machine routes back to coding — you will receive a new instruction at that point.
                    Governance: {context.Epic.EpicGovernancePath}
                    """);
        }

        if (spec.IsCodeReviewApproved == false)
        {
            spec.IsCodeDone = false;
            spec.IsCodeReviewApproved = null;

            return new CodingSpecState();
        }

        if (context.IsACRequired)
        {
            return new AcSpecState();
        }

        return new DoneSpecState();
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
