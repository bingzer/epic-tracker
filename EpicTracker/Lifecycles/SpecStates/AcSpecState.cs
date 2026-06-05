namespace EpicTracker.Lifecycles.SpecStates;

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
            return Exit(
                context: context,
                instruction: $"""
                    Hand off spec {spec.Id} to {spec.AssignedAgentName} via tmux-broker to run the AC checklist in the spec doc at {spec.SpecDocPath}.
                    Tell {spec.AssignedAgentName} to reply via tmux-broker with results when done.
                    Wait for their reply, then call update_spec({spec.Id}, IsAcPassed, true/false). That automatically advances the spec.
                    """
            );
        }

        if (spec.IsAcPassed == false)
        {
            return RaiseHumanInLoop(
                context: context,
                questions: $"AC failed for spec {spec.Id}. Approve to override and mark as done, or reject to send back to coding.",
                approveToStateName: DoneSpecState.StateName,
                rejectToStateName: CodingSpecState.StateName,
                instruction: $"""
                    AC failed. HumanInLoop raised for direction.
                    Call advance_spec("{spec.Id}") then wait for tmux to wake you.
                    """
            );
        }

        if (spec.NeedsHumanReview())
        {
            return RaiseHumanInLoop(
                context: context,
                questions: $"Spec {spec.Id} has passed AC. Please review and approve to mark as done.",
                approveToStateName: DoneSpecState.StateName,
                rejectToStateName: CodingSpecState.StateName,
                instruction: $"""
                    AC passed. HumanInLoop raised for final sign-off.
                    Call advance_spec("{spec.Id}") then wait for tmux to wake you.
                    """
            );
        }

        spec.ResetHumanApproval();
        return new DoneSpecState();
    }

    protected override bool UpdateSpecFieldAt(SpecContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Spec.IsAcPassed))
        {
            context.Spec.IsAcPassed = bool.Parse(value);
            return true;
        }

        return false;
    }
}
