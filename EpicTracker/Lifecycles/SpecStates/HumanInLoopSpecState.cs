namespace EpicTracker.Lifecycles.SpecStates;

internal class HumanInLoopSpecState : SpecState
{
    public const string StateName = "spec_human_in_loop";
    public override string Name => StateName;

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
            return Exit(
                context: context,
                instruction: $"""
                    Waiting for human response. Call advance_spec("{spec.Id}") then wait for tmux to wake you.
                    Governance: {context.Epic.EpicGovernancePath}
                    """
            );
        }

        var toStateName = spec.HumanInLoop.IsApproved == true
            ? spec.HumanInLoop.ApproveToStateName
            : spec.HumanInLoop.RejectToStateName;

        spec.ResetHumanApproval();

        if (toStateName == CodingSpecState.StateName)
        {
            spec.IsCodeDone = false;
        }

        return MoveTo(toStateName);
    }
}
