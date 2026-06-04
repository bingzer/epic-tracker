namespace EpicTracker.Lifecycles.EpicStates;

internal class HumanInLoopState : EpicState
{
    public override string Name => "human_in_loop";

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
            epic.SetEpicAgentInstruction("Waiting for human response. Do not call Advance() until the human has responded.");

            return this;
        }

        var toStateName = epic.HumanInLoop.IsApproved == true
            ? epic.HumanInLoop.ApproveToStateName
            : epic.HumanInLoop.RejectToStateName;

        epic.ResetHumanApproval($"Human {(epic.HumanInLoop.IsApproved == true ? "approved" : "rejected")}. Routing to {toStateName}.");

        return EpicState.Create(toStateName);
    }
}

