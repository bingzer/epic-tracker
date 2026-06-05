namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Reviewer agent reviews the implementation. Blocks until IsCodeReviewApproved is set.
/// Rejection resets IsCodeDone and returns to coding.
/// </summary>
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
            spec.SetEpicAgentInstruction($"""
                Coordinate a direct code review between the coding agent and reviewer — do not relay messages.
                1. Tell {spec.AssignedAgentId} to send their deliverables and spec context directly to {spec.ReviewerAgentName} via tmux-broker.
                2. Tell {spec.ReviewerAgentName} to review the implementation at {spec.SpecDocPath} and send their verdict (APPROVED or REJECTED with notes) directly back to you (the PM) via tmux-broker — not to the coding agent.
                Wait for {spec.ReviewerAgentName}'s verdict, then call update_spec(IsCodeReviewApproved, true/false) and advance_spec.
                """);

            return this;
        }

        if (spec.IsCodeReviewApproved == false)
        {
            spec.IsCodeDone = false;
            spec.IsCodeReviewApproved = null;

            spec.SetEpicAgentInstruction($"""
                Review rejected by {spec.ReviewerAgentName}. Hand back to coding agent {spec.AssignedAgentId} to address the feedback.
                Tell {spec.AssignedAgentId} to fix the issues and report back to you when done, then call UpdateSpec to set IsCodeDone = true and call AdvanceSpec.
                """);

            return new CodingSpecState();
        }

        spec.SetEpicAgentInstruction($"Review approved by {spec.ReviewerAgentName}. Proceeding to acceptance criteria.");

        return new AcSpecState();
    }
}
