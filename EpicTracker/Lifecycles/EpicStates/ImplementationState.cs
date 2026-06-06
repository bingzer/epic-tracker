using EpicTracker.Lifecycles.SpecStates;

namespace EpicTracker.Lifecycles.EpicStates;

internal class ImplementationState : EpicState
{
    public const string StateName = "implementation";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;
        epic.LastKnownStateName = Name;

        epic.ResetHumanApproval();

        foreach (var spec in epic.Specs.Where(s => !s.IsAbandoned && s.CurrentStateName == DraftingSpecState.StateName))
        {
            spec.CurrentStateName = ReadySpecState.StateName;
        }

        var pending = epic.Specs.Where(s => !s.IsAbandoned && s.CurrentStateName != DoneSpecState.StateName).ToList();

        if (pending.Count > 0)
        {
            var pendingList = string.Join("\n", pending.Select(s => $"- {s.Id} ({s.AssignedAgentName}): {s.CurrentStateName}"));

            return Exit(
                context: context,
                instruction: $"""
                    Specs still in progress:
                    {pendingList}
                    Do NOT message coding agents. The human controls when each spec starts via the "Code Now" button in the dashboard.
                    Poll spec states via get_epic, then call advance("{epic.Id}") once all specs reach "done".
                    """);
        }

        var specList = string.Join("\n", epic.Specs.Where(s => !s.IsAbandoned).Select(s => $"- {s.Id} ({s.AssignedAgentName}): {s.SpecDocPath}"));
        var deliverablePath = $"{epic.OutputDirectory}/deliverable.md";

        return RaiseHumanInLoop(
            context: context,
            questions: $"""
                All specs are done. A deliverable summary has been compiled at {deliverablePath} — review it to see what changed and how to verify the work.

                Approve to close the epic, or reject to return to spec writing.

                Specs:
                {specList}
                """,
            approveToStateName: ClosedState.StateName,
            rejectToStateName: SpecWritingState.StateName,
            instruction: $"""
                All specs are done. Before raising human review, you must compile a deliverable summary:

                1. Message each coding agent via tmux asking them to provide a short summary of:
                   - What they built or changed
                   - Which files were created or modified (absolute paths)
                   - How a human reviewer can verify their work (what to run, open, or check)
                2. Collect their replies.
                3. Write {deliverablePath} following the Deliverables format in governance.md. One section per spec.
                4. Call advance("{epic.Id}") — this will raise human-in-loop pointing to the deliverable.

                Do not call advance until the deliverable file is written.
                """
        );
    }
}
