namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Blocks until a human responds to Spec.HumanInLoop. Routes to ApproveToStateName or RejectToStateName.
/// </summary>
internal class HumanInLoopSpecState : SpecState
{
    public override string Name => "spec_human_in_loop";

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        if (spec.HumanInLoop is null)
        {
            throw new InvalidOperationException($"HumanInLoopSpecState entered but Spec.HumanInLoop is null for spec {spec.Id}.");
        }

        if (spec.HumanInLoop.IsApproved is null)
        {
            spec.SetEpicAgentInstruction("Waiting for human response.");

            return this;
        }

        var toStateName = spec.HumanInLoop.IsApproved == true
            ? spec.HumanInLoop.ApproveToStateName
            : spec.HumanInLoop.RejectToStateName;

        spec.HumanInLoop = null;

        return SpecState.Create(toStateName);
    }
}
