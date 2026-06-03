namespace EpicTracker.Lifecycles.EpicStates;

internal class WaterproofingState : EpicState
{
    public override string Name => "waterproofing";

    public override async Task<EpicState> MoveNext(Epic epic, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (epic.NeedsMockup && !epic.IsMockupDone)
        {
            epic.SetEpicAgentInstruction("Mockup required before waterproofing. Proceeding to mockup.");

            return new MockupState();
        }

        if (epic.NeedsAgentSwarm())
        {
            epic.RaiseAgentSwarm(
                objective: $"Read the epic document at {epic.EpicDocumentPath} and reach agreement on scope and responsibilities.",
                toStateName: new SpecWritingState().Name,
                instruction: $"""
                    Agent swarm raised for waterproofing alignment.
                    Message each coding agent via tmux, ask them to read the epic document and reply AGREE or DISAGREE with a note on their scope.
                    Call submit_agreement for each agent on their behalf, then call Advance.
                    """
            );

            return new AgentSwarmState();
        }

        epic.ResetAgentSwarm("All agents have reached consensus. Proceed to spec writing.");

        return new SpecWritingState();
    }
}

