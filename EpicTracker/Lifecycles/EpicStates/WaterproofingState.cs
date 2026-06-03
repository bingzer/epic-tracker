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

        if (epic.AgentSwarm is null)
        {
            epic.SetEpicAgentInstruction($"""
                All agents must align on scope and responsibilities before spec writing can begin.
                Raise an agent swarm with objective: "Read the epic document at {epic.EpicDocumentPath} and reach agreement on scope and responsibilities."
                Set toStateName to "{new SpecWritingState().Name}". Include all coding agents and yourself.
                """);

            return this;
        }

        if (!epic.AgentSwarm.HasConsensus)
        {
            epic.SetEpicAgentInstruction("Swarm in progress. Collect agreements from all agents.");

            return new AgentSwarmState();
        }

        epic.AgentSwarm = null;

        epic.SetEpicAgentInstruction("All agents have reached consensus. Proceed to spec writing.");

        return new SpecWritingState();
    }
}

