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
                    # Code Review — Iteration {spec.CodeReviewIterations + 1}

                    Send {spec.ReviewerAgentName} a review assignment via tmux-broker with:
                      - Spec doc: {spec.SpecDocPath}
                      - Implemented by: {spec.AssignedAgentName}
                      - Output directory: {context.Epic.OutputDirectory}
                      - Review against: ## Acceptance Criteria in the spec doc
                      - Reply to you (epic agent) with one of:
                          SPEC {spec.Id} STATUS: reviewing
                          SPEC {spec.Id} STATUS: review-approved
                          SPEC {spec.Id} STATUS: review-rejected REASON: <reason>

                    When their reply arrives:
                      reviewing          → no action
                      review-approved    → call update_spec({spec.Id}, IsCodeReviewApproved, true)
                      review-rejected    → call update_spec({spec.Id}, IsCodeReviewApproved, false)

                    Governance: {context.Epic.EpicGovernancePath}
                    """);
        }

        if (spec.IsCodeReviewApproved == false)
        {
            spec.CodeReviewIterations++;

            if (spec.CodeReviewIterations >= 5)
            {
                return RaiseHumanInLoop(
                    context: context,
                    questions: $"Spec {spec.Id} has failed code review {spec.CodeReviewIterations} times. Approve to override and continue to AC, or reject to send back to coding.",
                    approveToStateName: AcSpecState.StateName,
                    rejectToStateName: CodingSpecState.StateName,
                    instruction: $"""
                        Code review has failed {spec.CodeReviewIterations} times for spec {spec.Id}. HumanInLoop raised for direction.
                        Call advance_spec("{spec.Id}") then wait for tmux to wake you.
                        Governance: {context.Epic.EpicGovernancePath}
                        """
                );
            }

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
