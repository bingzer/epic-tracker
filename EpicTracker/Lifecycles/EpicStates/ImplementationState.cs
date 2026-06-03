namespace EpicTracker.Lifecycles.EpicStates;

/// <summary>
/// Watches all specs until every one reaches "done", then raises a HumanInLoop for final sign-off before closing.
/// Rejection sends the epic back to spec_writing to re-evaluate the spec list.
/// </summary>
internal class ImplementationState : EpicState
{
    public override string Name => "implementation";

    public override async Task<EpicState> MoveNext(Epic epic, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        foreach (var spec in epic.Specs)
        {
            spec.IsSpecApproved = true;
        }

        var drafting = epic.Specs.Where(s => s.CurrentStateName == "spec_drafting").ToList();

        if (drafting.Count > 0)
        {
            var instructions = string.Join("\n",
                drafting.Select(s => $"- Send {s.AssignedAgentId} the spec at {s.SpecDocPath} and tell them to implement it and report back when done."));

            epic.SetEpicAgentInstruction($"""
                Instruct each coding agent to begin implementation. Once they confirm done, call advance_spec on their behalf, then call advance.
                {instructions}
                """);

            return this;
        }

        var allDone = epic.Specs.All(s => s.CurrentStateName == "done");

        if (!allDone)
        {
            var pending = string.Join("\n", epic.Specs
                .Where(s => s.CurrentStateName != "done")
                .Select(s => $"- {s.Id} ({s.AssignedAgentId}): {s.CurrentStateName}"));

            epic.SetEpicAgentInstruction($"""
                Specs still in progress:
                {pending}
                Ping each coding agent above for a status update.
                Call Advance when all specs reach "done".
                """);

            return this;
        }

        var specList = string.Join("\n", epic.Specs.Select(s => $"- {s.Id} ({s.AssignedAgentId}): {s.SpecDocPath}"));

        epic.HumanInLoop = new HumanInLoop
        {
            Questions = $"All specs are done. Please review and approve to close the epic, or reject to return to spec writing.\n\nSpecs:\n{specList}",
            ApproveToStateName = new ClosedState().Name,
            RejectToStateName = new SpecWritingState().Name
        };

        epic.SetEpicAgentInstruction("All specs done. Raised HumanInLoop for final sign-off.");

        return new HumanInLoopState();
    }
}
