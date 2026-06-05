namespace EpicTracker.Lifecycles.EpicStates;

internal class HumanInLoopState : EpicState
{
    public const string StateName = "human_in_loop";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (epic.HumanInLoop is null)
        {
            throw new InvalidOperationException("HumanInLoop must be set on the epic before calling MoveNext.");
        }

        if (epic.HumanInLoop.IsApproved is null)
        {
            return Exit(
                context: context,
                instruction: $"Waiting for human response. Do not call advance(\"{epic.Id}\") until the human has responded."
            );
        }
        
        // Do not clear HumanInLoop here — the target state reads IsHumanApproved/IsHumanRejected and clears it.

        var toStateName = epic.HumanInLoop.IsApproved == true
            ? epic.HumanInLoop.ApproveToStateName
            : epic.HumanInLoop.RejectToStateName;

        return MoveTo(toStateName);
    }
}

