using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.EpicStates;

internal class HumanInLoopState : EpicState
{
    public override string Name => "human_in_loop";

    protected override async Task<EpicState> Next(Epic epic, ILogger logger, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

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

        epic.SetEpicAgentInstruction($"Human responded. Routing to {toStateName}.");

        return EpicState.Create(toStateName);
    }
}

