namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Coding agent runs the AC checklist from the spec doc, then human signs off before marking done.
/// </summary>
internal class AcSpecState : SpecState
{
    public const string StateName = "ac";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        if (spec.IsAcPassed is null)
        {
            spec.SetEpicAgentInstruction($"""
                Hand off spec {spec.Id} to coding agent {spec.AssignedAgentId} to run the AC checklist in the spec doc at {spec.SpecDocPath}.
                Tell {spec.AssignedAgentId} to report back with results when done.
                You are handing off — do not follow up. Wait for {spec.AssignedAgentId} to report back, then call UpdateSpec to set IsAcPassed = true or false and call AdvanceSpec.
                """);

            return this;
        }

        if (spec.IsAcPassed == false)
        {
            spec.IsAcPassed = null;
            spec.IsCodeDone = false;

            spec.SetEpicAgentInstruction($"""
                AC failed for spec {spec.Id}. Hand back to coding agent {spec.AssignedAgentId} to fix the failing criteria.
                Tell {spec.AssignedAgentId} to report back when done, then call UpdateSpec to set IsCodeDone = true and call AdvanceSpec.
                """);

            return new CodingSpecState();
        }

        if (spec.HumanInLoop is null || spec.HumanInLoop.IsApproved is null)
        {
            spec.HumanInLoop = new HumanInLoop
            {
                Questions = $"Spec {spec.Id} has passed AC. Please review and approve to mark as done.",
                ApproveToStateName = DoneSpecState.StateName,
                RejectToStateName = CodingSpecState.StateName
            };

            spec.SetEpicAgentInstruction("AC passed. Raised HumanInLoop for final sign-off.");

            return new HumanInLoopSpecState();
        }

        if (spec.HumanInLoop.IsApproved == false)
        {
            spec.IsAcPassed = null;
            spec.IsCodeDone = false;
            spec.HumanInLoop = null;

            spec.SetEpicAgentInstruction($"Human rejected AC sign-off. Hand back to {spec.AssignedAgentId} to rework.");

            return new CodingSpecState();
        }

        spec.HumanInLoop = null;

        spec.SetEpicAgentInstruction($"Spec {spec.Id} approved. Marking as done.");

        return new DoneSpecState();
    }
}
