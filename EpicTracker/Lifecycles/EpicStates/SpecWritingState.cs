using EpicTracker.Lifecycles.SpecStates;

namespace EpicTracker.Lifecycles.EpicStates;

internal class SpecWritingState : EpicState
{
    public const string StateName = "spec_writing";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;
        epic.LastKnownStateName = Name;

        if (epic.IsHumanRejected())
        {
            var rejectionReason = epic.HumanInLoop?.HumanInput;
            var rejectionNote = string.IsNullOrWhiteSpace(rejectionReason)
                ? "No specific reason was given."
                : $"Reason: {rejectionReason}";

            epic.SpecWritingPhase = 1;
            epic.AbandonAllSpecs();
            epic.ResetAgentSwarm();
            epic.ResetHumanApproval();

            return Exit(
                context: context,
                instruction: $"""
                    Human rejected the spec list. All specs abandoned. SpecWritingPhase reset to 1. {rejectionNote}
                    Start fresh — call advance("{epic.Id}") to begin phase 1.
                    """
            );
        }

        if (epic.SpecWritingPhase <= 1)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Post kickoff message to the epic channel. Ask all coding agents to reply with their session name, what they own, and their cwd. Once all agents have replied, write `## Agents` in the epic doc using their responses (format: `- agentname: role — domain description. cwd: /path`). Then call `update_epic(SpecWritingPhase, 2)`.

                    Log in epic.md under ## Spec Writing — Phase 1: list agents who self-reported and their domains.

                    Epic directory: {epic.EpicDirectory}
                    Epic doc: {epic.EpicDocumentPath}
                    Governance: {epic.EpicGovernancePath}
                    """
            );
        }

        if (epic.SpecWritingPhase == 2)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Tell all coding agents to write their spec doc and post the path to the channel. Instruct them to read governance.md and follow the spec template exactly before writing.

                    Wait for all coding agents to post their spec paths to the channel. For each spec, review it for Goldilocks size before registering (one concern, one layer, testable in isolation once deps met — challenge specs that violate this). Call `create_spec` for each approved spec — pass a clean human-readable title as `specName` (e.g. 'Auth Flow'), never append a counter or suffix (the backend handles slug uniqueness). Post the full spec list (name, path, assigned agent) back to the channel. Then call `update_epic(SpecWritingPhase, 3)`.

                    Log in epic.md under ## Spec Writing — Phase 2: specs registered and any Goldilocks rejections.

                    Epic directory: {epic.EpicDirectory}
                    Epic doc: {epic.EpicDocumentPath}
                    Governance: {epic.EpicGovernancePath}
                    """
            );
        }

        if (epic.SpecWritingPhase == 3)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Agents are now reading all specs in parallel and signing `[x]` or blocking `[-]` directly in spec files. Do not intervene. Wait for every coding agent to post `DONE READING` to the channel. Once all agents have confirmed, call `update_epic(SpecWritingPhase, 4)`.

                    Log in epic.md under ## Spec Writing — Phase 3: which agents signed and any blocks raised.

                    Epic directory: {epic.EpicDirectory}
                    Epic doc: {epic.EpicDocumentPath}
                    Governance: {epic.EpicGovernancePath}
                    """
            );
        }

        if (epic.SpecWritingPhase == 4)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Collect all concerns agents posted to the channel. For each concern: if a spec edit is needed, pm makes the edit (not the agent), then resets all `[x]` to `[ ]` in that spec's `## Reviewer` section and posts to channel notifying agents to re-review. If a new spec is needed, call `create_spec`. If a spec should be dropped, call `update_spec` to abandon it. Wait for every coding agent to post `NO MORE CONCERNS` to the channel. Then call `update_epic(SpecWritingPhase, 5)`.

                    Log in epic.md under ## Spec Writing — Phase 4: concerns raised, specs edited, specs added/dropped.

                    Epic directory: {epic.EpicDirectory}
                    Epic doc: {epic.EpicDocumentPath}
                    Governance: {epic.EpicGovernancePath}
                    """
            );
        }

        var failures = new List<string>();
        var activeSpecs = epic.Specs.Where(s => !s.IsAbandoned).ToList();

        foreach (var spec in activeSpecs)
        {
            if (!context.FileSystem.FileExists(spec.SpecDocPath))
            {
                failures.Add($"Spec '{spec.Id}' file not found at {spec.SpecDocPath}");
                continue;
            }

            var content = context.FileSystem.ReadAllText(spec.SpecDocPath!);
            var reviewerItems = MarkdownChecklist.Parse(content, "## Reviewer");

            if (reviewerItems.Count == 0)
            {
                failures.Add($"Spec '{spec.Id}' is missing ## Reviewer section or has no entries");
                continue;
            }

            foreach (var agentName in epic.CodingAgentNames)
            {
                var signed = reviewerItems.Any(item =>
                    item.IsChecked &&
                    string.Equals(item.Name, agentName, StringComparison.OrdinalIgnoreCase));

                if (!signed)
                {
                    failures.Add($"Spec '{spec.Id}': {agentName} has not approved");
                }
            }
        }

        if (failures.Count > 0)
        {
            var failureList = string.Join("\n", failures.Select(f => $"- {f}"));
            return Exit(
                context: context,
                instruction: $"""
                    Spec review gate failed. Resolve all issues then call advance("{epic.Id}"):
                    {failureList}

                    Epic directory: {epic.EpicDirectory}
                    Epic doc: {epic.EpicDocumentPath}
                    Governance: {epic.EpicGovernancePath}
                    """
            );
        }

        if (epic.NeedsHumanReview())
        {
            var specList = string.Join("\n", activeSpecs.Select(s =>
                $"- {(s.SpecName is not null ? $"{s.SpecName} ({s.Id})" : s.Id)}"));

            return RaiseHumanInLoop(
                context: context,
                questions: $"All coding agents have signed off on all specs. Please review the final spec list and approve to proceed to implementation.\n\nSpecs:\n{specList}",
                approveToStateName: ImplementationState.StateName,
                rejectToStateName: Name,
                instruction: $"""
                    All coding agents have signed off on all specs. HumanInLoop raised for final review. Call advance("{epic.Id}") then wait for tmux to wake you.

                    Log in epic.md under ## Spec Writing — Phase 5: all agents signed off, advancing to implementation.

                    Epic directory: {epic.EpicDirectory}
                    Epic doc: {epic.EpicDocumentPath}
                    Governance: {epic.EpicGovernancePath}
                    """
            );
        }

        return new ImplementationState();
    }
}
