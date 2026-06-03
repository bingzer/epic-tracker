namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Reviewer agent reviews the implementation. Blocks until IsCodeReviewApproved is set.
/// Rejection resets IsCodeDone and returns to coding.
/// </summary>
internal class CodeReviewSpecState : SpecState
{
    public override string Name => "code_review";

    public override async Task<SpecState> MoveNext(Spec spec, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (spec.IsCodeReviewApproved is null)
        {
            spec.SetEpicAgentInstruction($"""
                Hand off spec {spec.Id} to reviewer agent {spec.ReviewerAgentId} for code review.
                Tell {spec.ReviewerAgentId} to review the implementation by {spec.AssignedAgentId} at {spec.SpecDocPath} and report back to you when done.
                You are handing off — do not follow up. Wait for {spec.ReviewerAgentId} to report back, then call UpdateSpec to set IsCodeReviewApproved = true or false, then call AdvanceSpec.
                """);

            return this;
        }

        if (spec.IsCodeReviewApproved == false)
        {
            spec.IsCodeDone = false;
            spec.IsCodeReviewApproved = null;

            spec.SetEpicAgentInstruction($"""
                Review rejected by {spec.ReviewerAgentId}. Hand back to coding agent {spec.AssignedAgentId} to address the feedback.
                Tell {spec.AssignedAgentId} to fix the issues and report back to you when done, then call UpdateSpec to set IsCodeDone = true and call AdvanceSpec.
                """);

            return new CodingSpecState();
        }

        spec.SetEpicAgentInstruction($"Review approved by {spec.ReviewerAgentId}. Proceeding to acceptance criteria.");

        return new AcSpecState();
    }
}
