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
                DO NOT write any spec documents yourself. Your job is to coordinate — message each coding agent via tmux and ask them to write their own spec doc.

                Step-by-step:
                1. Send each coding agent a tmux message asking them to write a spec document for their area of responsibility, following the governance format at {epic.EpicGovernancePath}.
                2. Wait for each agent to reply with an absolute file path and a short spec name (e.g. 'auth-flow').
                3. Only after receiving their reply, call create_spec with the path and agent ID they provided.
                4. Once all agents have responded and all specs are registered, call Advance.

                Agents: {agentList}
                Tell each agent: specs should be Goldilocks-sized — not too big (one spec per concern), not too small (don't split trivial changes). One spec per output file is a good heuristic.
                Tell each agent: save the spec file using an absolute path and report back with the absolute path (e.g. C:\Users\... or /home/...) — relative paths will be rejected.
                Do NOT dispatch any coding work yet — this is the spec writing phase only.

                After calling Advance, the state machine will:
                - Raise an agent swarm asking all agents to review and agree on the full spec list. You will collect AGREE/DISAGREE responses and call submit_agreement for each, then Advance again.
                - Then check that each spec document actually exists on disk. If any are missing, you will be asked to follow up with the relevant agent.
                - Then raise a HumanInLoop for final human sign-off before implementation begins.
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

            var rejectionReason = epic.HumanInLoop?.HumanInput;
            var rejectionNote = string.IsNullOrWhiteSpace(rejectionReason)
                ? "No specific reason was given."
                : $"Reason: {rejectionReason}";

            epic.AgentSwarm = null;
            epic.ResetHumanApproval($"Human rejected the spec list. All specs abandoned. {rejectionNote} Instruct coding agents to start fresh with this feedback in mind, then call Advance.");

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
