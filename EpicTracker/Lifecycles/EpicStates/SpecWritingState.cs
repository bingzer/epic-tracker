namespace EpicTracker.Lifecycles.EpicStates;

internal class SpecWritingState : EpicState
{
    public const string StateName = "spec_writing";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (epic.Specs.All(s => s.IsAbandoned))
        {
            var agentList = string.Join(", ", epic.CodingAgentNames);
            return Exit(
                context: context,
                instruction: $"""
                    Read the epic document at {epic.EpicDocumentPath}.
                    DO NOT write any spec documents yourself. Your job is to coordinate — message each coding agent via tmux and ask them to write their own spec doc.

                    Step-by-step:
                    1. Send each coding agent a tmux message asking them to write a spec document for their area of responsibility, following the governance format at {epic.EpicGovernancePath}.
                    2. Wait for each agent to reply with an absolute file path and a short spec name (e.g. 'auth-flow').
                    3. Only after receiving their reply, call create_spec with the path and agent ID they provided.
                    4. Once all agents have responded and all specs are registered, call advance("{epic.Id}").

                    Agents: {agentList}
                    Tell each agent: specs should be Goldilocks-sized — not too big (one spec per concern), not too small (don't split trivial changes). One spec per output file is a good heuristic.
                    Tell each agent: save the spec file using an absolute path and report back with the absolute path (e.g. C:\Users\... or /home/...) — relative paths will be rejected.
                    Do NOT dispatch any coding work yet — this is the spec writing phase only.
                    """
            );
        }

        var pendingSpecs = epic.Specs.Where(s => !s.IsSpecApproved && !s.IsAbandoned).ToList();

        if (pendingSpecs.Count > 0 && epic.NeedsAgentSwarm())
        {
            var specList = string.Join("\n", pendingSpecs.Select(s => $"- {s.Id}: {s.SpecDocPath}"));

            return RaiseAgentSwarm(
                context: context,
                objective: $"""
                    Review all pending specs together. Reach consensus on the final spec list.
                    If a spec should be abandoned, tell the epic agent the spec ID and it will call update_spec to mark it abandoned.
                    If new specs are needed that were not thought of, tell the epic agent and it will call create_spec to register them.
                    Once all changes are made, all agents must agree the spec list is complete and correct.
                    Pending specs:
                    {specList}
                    """,
                whenApprovedStateName: Name,
                instruction: $"""
                    Agent swarm raised to review all specs.
                    Follow the governance document at {epic.EpicGovernancePath} for swarm instructions.
                    Call advance("{epic.Id}") after submitting all agreements.
                    Do NOT dispatch any coding work — this is the spec writing phase only. Remind coding agents not to begin coding until told.
                    """
            );
        }

        if (pendingSpecs.Count > 0 && epic.AgentSwarmHasConsensus())
        {
            epic.ApproveAllSpecs();
            epic.ResetAgentSwarm();
        }

        if (epic.NeedsHumanReview())
        {
            var specList = string.Join("\n", epic.Specs.Where(s => !s.IsAbandoned).Select(s => $"- {s.Id}: {s.SpecDocPath}"));

            return RaiseHumanInLoop(
                context: context,
                questions: $"All specs have been reviewed and approved by agents. Please review the final spec list in the dashboard and approve to proceed to implementation.\n\nSpecs:\n{specList}",
                approveToStateName: ImplementationState.StateName,
                rejectToStateName: Name,
                instruction: $"""
                    All specs approved by agents. HumanInLoop raised for final human review.
                    Call advance("{epic.Id}") then wait for tmux to wake you.
                    Do NOT dispatch any coding work — this is the spec writing phase only. Remind coding agents not to begin coding until told.
                    """
            );
        }

        if (epic.IsHumanRejected())
        {
            var rejectionReason = epic.HumanInLoop?.HumanInput;
            var rejectionNote = string.IsNullOrWhiteSpace(rejectionReason)
                ? "No specific reason was given."
                : $"Reason: {rejectionReason}";

            epic.AbandonAllSpecs();
            epic.ResetAgentSwarm();
            epic.ResetHumanApproval();

            return Exit(
                context: context,
                instruction: $"""
                    Human rejected the spec list. All specs abandoned. {rejectionNote}
                    Instruct coding agents to start fresh with this feedback in mind, then call advance("{epic.Id}").
                    """
            );
        }

        var missingSpecs = epic.Specs.Where(s => !s.IsAbandoned && !context.FileSystem.FileExists(s.SpecDocPath)).ToList();
        if (missingSpecs.Count > 0)
        {
            var missingList = string.Join("\n", missingSpecs.Select(s => $"- {s.Id} ({s.AssignedAgentName}): {s.SpecDocPath}"));

            return Exit(
                context: context,
                instruction: $"""
                    Not all spec documents can be found. Please ensure each coding agent has written their spec and the file paths are correct.
                    Missing specs:
                    {missingList}
                    Once all specs are confirmed to exist, call advance("{epic.Id}").
                    """
            );
        }

        return new ImplementationState();
    }
}
