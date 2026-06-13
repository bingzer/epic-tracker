namespace EpicTracker.Lifecycles.SpecStates;

internal class AcSpecState : SpecState
{
    public const string StateName = "ac";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        spec.LastKnownStateName = Name;

        if (spec.IsAcPassed is null)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Hand off spec {spec.Id} to {spec.AssignedAgentName} via tmux-broker to run the AC checklist in the spec doc at {spec.SpecDocPath}.
                    Tell {spec.AssignedAgentName} to tick each item in the ## Acceptance Criteria section as they verify it (change - [ ] to - [x]). All items must be checked — the state machine enforces this before IsAcPassed can be set.
                    Tell {spec.AssignedAgentName} to reply via tmux-broker with pass/fail results when done.
                    Wait for their reply, then call update_spec({spec.Id}, IsAcPassed, true/false). That automatically advances the spec.
                    Governance: {context.Epic.EpicGovernancePath}
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
                    Governance: {context.Epic.EpicGovernancePath}
                    """
            );
        }

        if (HasUncheckedItems(context, out var acItems))
        {
            return Exit(
                context: context,
                instruction: $"""
                    Cannot advance spec {spec.Id} — ## Acceptance Criteria has unchecked items:
                    {acItems}
                    Tell {spec.AssignedAgentName} to tick all items before marking IsAcPassed again.
                    Governance: {context.Epic.EpicGovernancePath}
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
                    Governance: {context.Epic.EpicGovernancePath}
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

    private static bool HasUncheckedItems(SpecContext context, out string uncheckedItems)
    {
        uncheckedItems = string.Empty;

        if (context.Spec.SpecDocPath is null || !context.FileSystem.FileExists(context.Spec.SpecDocPath))
        {
            return false;
        }

        var content = context.FileSystem.ReadAllText(context.Spec.SpecDocPath);
        var unchecked_ = MarkdownChecklist.Parse(content, "## Acceptance Criteria").Where(i => !i.IsChecked).ToList();

        if (unchecked_.Count == 0)
        {
            return false;
        }

        uncheckedItems = string.Join("\n", unchecked_.Select(i => $"  - {i.Name}"));
        return true;
    }
}
