using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.EpicStates;

internal class SpecWritingState : EpicState
{
    public override string Name => "spec_writing";

    protected override async Task<EpicState> Next(Epic epic, ILogger logger, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (epic.Specs.All(s => s.IsAbandoned))
        {
            var agentList = string.Join(", ", epic.CodingAgents);

            epic.SetEpicAgentInstruction($"""
                Read the epic document at {epic.EpicDocumentPath}.
                Instruct each coding agent (via tmux) to write a spec document under the epic path and send you the path when done.
                Agents: {agentList}
                For each agent that sends you a spec path, call create_spec to register it.
                Once all agents have responded, call Advance.
                Do NOT dispatch any coding work yet — this is the spec writing phase only.
                """);

            return this;
        }

        var pendingSpecs = epic.Specs.Where(s => !s.IsSpecApproved && !s.IsAbandoned).ToList();
        var shouldRaiseSwarm = pendingSpecs.Count > 0 && epic.NeedsAgentSwarm();
        if (shouldRaiseSwarm)
        {
            var specList = string.Join("\n", pendingSpecs.Select(s => $"- {s.Id}: {s.SpecDocPath}"));

            epic.RaiseAgentSwarm(
                objective: $"""
                    Review all pending specs together. Reach consensus on the final spec list.
                    If a spec should be abandoned, tell the epic agent the spec ID and it will call update_spec to mark it abandoned.
                    If new specs are needed that were not thought of, tell the epic agent and it will call create_spec to register them.
                    Once all changes are made, all agents must agree the spec list is complete and correct.
                    Pending specs:
                    {specList}
                    """,
                toStateName: Name,
                instruction: $"""
                    Agent swarm raised to review all specs.
                    Message each coding agent via tmux, ask them to review the specs and reply AGREE or DISAGREE with a note.
                    Call submit_agreement for each agent on their behalf, then call Advance.
                    """
            );

            return new AgentSwarmState();
        }

        var swarmReachedConsensus = pendingSpecs.Count > 0 && epic.AgentSwarmHasConsensus();
        if (swarmReachedConsensus)
        {
            foreach (var spec in pendingSpecs)
            {
                spec.IsSpecApproved = true;
            }

            epic.ResetAgentSwarm("All specs approved by agent swarm. Call Advance to continue.");
            // falls through to NeedsHumanReview
        }

        if (epic.NeedsHumanReview())
        {
            var specList = string.Join("\n", epic.Specs.Where(s => !s.IsAbandoned).Select(s => $"- {s.Id}: {s.SpecDocPath}"));

            epic.RaiseHumanInLoop(
                questions: $"All specs have been reviewed and approved by agents. Please review the final spec list in the dashboard and approve to proceed to implementation.\n\nSpecs:\n{specList}",
                approveToStateName: new ImplementationState().Name,
                rejectToStateName: Name,
                instruction: "All specs approved by agents. HumanInLoop raised for final human review. Wait for further instruction."
            );

            return new HumanInLoopState();
        }

        if (epic.IsHumanRejected())
        {
            foreach (var spec in epic.Specs)
            {
                spec.IsAbandoned = true;
            }

            epic.ResetHumanApproval("Human rejected the spec list. All specs abandoned. Instruct coding agents to start fresh, then call Advance.");

            return this;
        }

        return new ImplementationState();
    }
}
