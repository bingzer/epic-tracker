namespace EpicTracker.Lifecycles.EpicStates;

internal class SpecWritingState : EpicState
{
    public override string Name => "spec_writing";

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (epic.Specs.All(s => s.IsAbandoned))
        {
            var agentList = string.Join(", ", epic.CodingAgents);

            epic.SetEpicAgentInstruction($"""
                Read the epic document at {epic.EpicDocumentPath}.
                Instruct each coding agent (via tmux) to write a spec document under the epic path, following the governance format at {epic.EpicGovernancePath}, and send you the path and a short spec name (e.g. 'auth-flow') when done.
                Agents: {agentList}
                For each agent that responds, call create_spec with their spec name, path, and agent ID to register it.
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

        // last check before advancing
        // check for file existence to ensure specs are actually written before moving on
        var allSpecsExist = epic.Specs.Where(s => !s.IsAbandoned).All(s => context.FileSystem.FileExists(s.SpecDocPath));
        if (!allSpecsExist)
        {
            var missingSpecs = epic.Specs.Where(s => !s.IsAbandoned && !context.FileSystem.FileExists(s.SpecDocPath)).ToList();
            var missingList = string.Join("\n", missingSpecs.Select(s => $"- {s.Id} ({s.AssignedAgentId}): {s.SpecDocPath}"));

            epic.SetEpicAgentInstruction($"""
                Not all spec documents can be found. Please ensure each coding agent has written their spec and the file paths are correct.
                Missing specs:
                {missingList}
                Once all specs are confirmed to exist, call Advance.
                """);

            return this;
        }
        
        return new ImplementationState();
    }
}
