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

            var hasReady = pending.Any(s => s.CurrentStateName == ReadySpecState.StateName);
            var hasCodeReview = pending.Any(s => s.CurrentStateName == CodeReviewSpecState.StateName);

            var notes = new List<string>();

            if (hasReady)
            {
                notes.Add("Specs in 'ready' are waiting for a human to click \"Code Now\" — do NOT message their coding agents.");
            }

            if (hasCodeReview)
            {
                notes.Add("Specs in 'code_review' are waiting for a reviewer assignment — advance_spec will give you the assignment to send.");
            }

            var hasHumanInLoop = pending.Any(s => s.CurrentStateName == HumanInLoopSpecState.StateName);

            if (hasHumanInLoop)
            {
                notes.Add("Specs in 'spec_human_in_loop' are paused waiting for a human decision in the dashboard — do not advance them.");
            }

            var notesBlock = notes.Count > 0 ? "\n" + string.Join("\n", notes) : string.Empty;

            return Exit(
                context: context,
                instruction: $"""
                    Specs still in progress:
                    {pendingList}{notesBlock}
                    Call advance_spec for each pending spec to get its instruction and drive it forward.
                    Once all specs reach "done", call advance("{epic.Id}").
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
