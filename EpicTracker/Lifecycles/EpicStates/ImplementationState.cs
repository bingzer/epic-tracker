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
                    Ping each coding agent above for a status update.
                    Call advance("{epic.Id}") when all specs reach "done".
                    """);
        }

        var specList = string.Join("\n", epic.Specs.Where(s => !s.IsAbandoned).Select(s => $"- {s.Id} ({s.AssignedAgentName}): {s.SpecDocPath}"));

        return RaiseHumanInLoop(
            context: context,
            questions: $"""
                All specs are done. Please review and approve to close the epic, or reject to return to spec writing.

                Specs:
                {specList}
                """,
            approveToStateName: ClosedState.StateName,
            rejectToStateName: SpecWritingState.StateName,
            instruction: $"""
                All specs done. HumanInLoop raised for final sign-off.
                Call advance("{epic.Id}") then wait for tmux to wake you.
                """
        );
    }
}
