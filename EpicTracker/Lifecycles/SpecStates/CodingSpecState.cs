using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Coding agent implements the spec. Blocks until IsCodeDone is true.
/// </summary>
internal class CodingSpecState : SpecState
{
    public override string Name => "coding";

    protected override async Task<SpecState> Next(Spec spec, ILogger logger, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (!spec.IsCodeDone)
        {
            spec.SetEpicAgentInstruction($"""
                Hand off spec {spec.Id} to coding agent {spec.AssignedAgentId} to implement the spec at {spec.SpecDocPath}.
                Tell {spec.AssignedAgentId} to report back to you when done.
                You are handing off — do not follow up. Wait for {spec.AssignedAgentId} to report back, then call UpdateSpec to set IsCodeDone = true and call AdvanceSpec.
                """);

            return this;
        }

        if (spec.CodeReviewRequired)
        {
            spec.SetEpicAgentInstruction($"Code complete by {spec.AssignedAgentId}. Proceeding to code review by {spec.ReviewerAgentId}.");

            return new CodeReviewSpecState();
        }

        spec.SetEpicAgentInstruction("Code complete. No review required. Proceeding to acceptance criteria.");

        return new AcSpecState();
    }
}
