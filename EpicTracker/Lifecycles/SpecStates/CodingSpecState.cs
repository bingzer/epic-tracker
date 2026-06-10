using EpicTracker.Services;

namespace EpicTracker.Lifecycles.SpecStates;

internal class CodingSpecState : SpecState
{
    public const string StateName = "coding";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        spec.LastKnownStateName = Name;

        if (spec.ScopeChange is not null)
        {
            var instruction = spec.ScopeChange.IsApproved switch
            {
                null  => $"""
                    Spec {spec.Id} has a pending scope change: "{spec.ScopeChange.Description}"
                    Raise this to the human via raise_human_in_loop on the EPIC (not the spec) to get approval before proceeding.
                    Once the human decides, call update_spec({spec.Id}, ScopeChangeApproved, true) if approved or update_spec({spec.Id}, ScopeChangeApproved, false) if rejected. That automatically unblocks the spec.
                    Governance: {context.Epic.EpicGovernancePath}
                    """,
                false => $"""
                    Scope change was REJECTED by human. Continue with the original spec scope for spec {spec.Id}.
                    Call advance_spec("{spec.Id}") to continue.
                    Governance: {context.Epic.EpicGovernancePath}
                    """,
                true  => $"""
                    Scope change was APPROVED by human for spec {spec.Id}.
                    Update the spec doc at {spec.SpecDocPath} to reflect the expanded scope, then call advance_spec("{spec.Id}") to continue.
                    Governance: {context.Epic.EpicGovernancePath}
                    """,
            };

            if (spec.ScopeChange.IsApproved is not null)
            {
                spec.ScopeChange = null;
            }

            return Exit(context: context, instruction: instruction);
        }

        if (!spec.IsCodeDone)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Hand off spec {spec.Id} to {spec.AssignedAgentName} via tmux-broker. Tell them to implement the spec at {spec.SpecDocPath}.
                    If this is a retry after a rejected code review, include the rejection reason from the last review in your assignment to the coding agent.
                    Output directory for this epic: {context.Epic.OutputDirectory}
                    Tell {spec.AssignedAgentName} to tick each item in the ## Development Plan section of the spec doc as they complete it (change - [ ] to - [x]).
                    When implementation is complete, {spec.AssignedAgentName} sends to you (epic agent): SPEC {spec.Id} STATUS: coding-done
                    When you receive that, call update_spec({spec.Id}, IsCodeDone, true). That automatically advances the spec.
                    Any testing done during implementation should be labeled as "implementation smoke test" — not as AC results. AC is formally verified in the ac state after code review.
                    Scope change protocol: Tell {spec.AssignedAgentName} — if they discover the work is larger than the spec describes, do NOT expand scope silently. Signal: SPEC {spec.Id} SCOPE CHANGE: <description>. You will flag it for human approval before they continue.
                    Governance: {context.Epic.EpicGovernancePath}
                    """
            );
        }

        if (HasUncheckedItems(context, out var devPlanItems))
        {
            return Exit(
                context: context,
                instruction: $"""
                    Cannot advance spec {spec.Id} — ## Development Plan has unchecked items:
                    {devPlanItems}
                    Tell {spec.AssignedAgentName} to tick all items before signaling coding-done again.
                    Governance: {context.Epic.EpicGovernancePath}
                    """
            );
        }

        if (context.IsCodeReviewRequired)
        {
            return new CodeReviewSpecState();
        }

        if (context.IsACRequired)
        {
            return new AcSpecState();
        }

        return new DoneSpecState();
    }

    protected override bool UpdateSpecFieldAt(SpecContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Spec.IsCodeDone))
        {
            context.Spec.IsCodeDone = bool.Parse(value);
            return true;
        }

        if (fieldName == nameof(Spec.ScopeChangeApproved))
        {
            context.Spec.ScopeChangeApproved = bool.Parse(value);
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
        var unchecked_ = MarkdownChecklist.Parse(content, "## Development Plan").Where(i => !i.IsChecked).ToList();

        if (unchecked_.Count == 0)
        {
            return false;
        }

        uncheckedItems = string.Join("\n", unchecked_.Select(i => $"  - {i.Name}"));
        return true;
    }

}
