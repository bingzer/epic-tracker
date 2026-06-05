using EpicTracker.Lifecycles.SpecStates;

namespace EpicTracker.Lifecycles.EpicStates;

/// <summary>
/// Watches all specs until every one reaches "done", then raises a HumanInLoop for final sign-off before closing.
/// Rejection sends the epic back to spec_writing to re-evaluate the spec list.
/// </summary>
internal class ImplementationState : EpicState
{
    public const string StateName = "implementation";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        epic.HumanInLoop = null;

        foreach (var spec in epic.Specs.Where(s => !s.IsAbandoned && s.CurrentStateName == DraftingSpecState.StateName))
        {
            spec.CurrentStateName = CodingSpecState.StateName;
        }

        var pending = epic.Specs.Where(s => s.CurrentStateName != DoneSpecState.StateName).ToList();
        if (pending.Count > 0)
        {
            var pendingList = string.Join("\n", pending.Select(s => $"- {s.Id} ({s.AssignedAgentId}): {s.CurrentStateName}"));

            epic.SetEpicAgentInstruction($"""
                Specs still in progress:
                {pendingList}
                Ping each coding agent above for a status update.
                Call Advance when all specs reach "done".
                """);

            return this;
        }

        if (epic.NeedsHumanReview())
        {
            var specList = string.Join("\n", epic.Specs.Select(s => $"- {s.Id} ({s.AssignedAgentId}): {s.SpecDocPath}"));

            epic.RaiseHumanInLoop(
                questions: $"""
                    All specs are done. Please review and approve to close the epic, or reject to return to spec writing.

                    Specs:
                    {specList}
                    """,
                approveToStateName: ClosedState.StateName,
                rejectToStateName: SpecWritingState.StateName,
                instruction: "All specs done. Raised HumanInLoop for final sign-off."
            );

            return new HumanInLoopState();
        }

        // human approved — HumanInLoopState routed here; return this to let the agent call Advance and close
        return this;
    }
}
