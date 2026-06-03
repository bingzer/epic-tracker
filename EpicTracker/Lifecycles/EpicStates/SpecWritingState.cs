namespace EpicTracker.Lifecycles.EpicStates;

internal class SpecWritingState : EpicState
{
    public override string Name => "spec_writing";

    public override async Task<EpicState> MoveNext(Epic epic, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (epic.HumanInLoop is not null && epic.HumanInLoop.IsApproved is null)
        {
            epic.SetEpicAgentInstruction("Waiting for human response. Do not call Advance until the human has responded via the dashboard.");

            return this;
        }

        if (epic.HumanInLoop?.IsApproved is not null)
        {
            if (epic.HumanInLoop.IsApproved == false)
            {
                foreach (var spec in epic.Specs)
                {
                    spec.IsAbandoned = true;
                }

                epic.AgentSwarm = null;
                epic.HumanInLoop = null;

                epic.SetEpicAgentInstruction("Human rejected the spec list. All specs abandoned. Instruct coding agents to re-submit specs from scratch, then call Advance.");

                return this;
            }

            var toStateName = epic.HumanInLoop.ApproveToStateName;

            epic.HumanInLoop = null;

            epic.SetEpicAgentInstruction($"Human approved. Proceeding to {toStateName}.");

            return EpicState.Create(toStateName);
        }

        if (epic.CodingAgents.Count == 0)
        {
            epic.SetEpicAgentInstruction("No coding agents assigned. Call update_epic with field CodingAgents (comma-separated agent ids) then call Advance.");

            return this;
        }

        if (epic.Specs.Count == 0)
        {
            var agentList = string.Join(", ", epic.CodingAgents);

            epic.SetEpicAgentInstruction($"""
                Instruct each coding agent to read the epic document at {epic.EpicDocumentPath}.
                Each agent must either write a spec document under the epic path and signal you with the path, or signal "no spec needed" if they have no development work.
                Agents: {agentList}
                For each agent that submits a spec, call create_spec to register it. Note which agents said no spec needed.
                Once all agents have responded, call Advance.
                If no agent submitted a spec, raise a HumanInLoop to confirm before proceeding.
                """);

            return this;
        }

        if (epic.AgentSwarm is null)
        {
            var specList = string.Join("\n", epic.Specs.Select(s => $"- {s.Id} ({s.AssignedAgentId}): {s.SpecDocPath}"));

            epic.SetEpicAgentInstruction($"""
                Specs submitted so far:
                {specList}
                Raise an agent swarm to review all specs — agents should agree on the final list, adding, removing, or modifying specs until consensus.
                Objective: "Review all submitted specs. Reach consensus on the final spec list for this epic."
                Set toStateName to "{Name}".
                """);

            return this;
        }

        if (!epic.AgentSwarm.HasConsensus)
        {
            return new AgentSwarmState();
        }

        epic.HumanInLoop = new HumanInLoop
        {
            Questions = $"Agents have reached consensus on the spec list. Please review and approve to proceed to development.\n\nSpecs:\n{string.Join("\n", epic.Specs.Select(s => $"- {s.Id} ({s.AssignedAgentId}): {s.SpecDocPath}"))}",
            ApproveToStateName = new ImplementationState().Name,
            RejectToStateName = Name
        };

        epic.SetEpicAgentInstruction("Specs approved by agents. Raised HumanInLoop for final sign-off. Call Advance to enter human_in_loop state.");

        return new HumanInLoopState();
    }
}
