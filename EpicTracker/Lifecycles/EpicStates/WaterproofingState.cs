namespace EpicTracker.Lifecycles.EpicStates;

internal class WaterproofingState : EpicState
{
    public const string StateName = "waterproofing";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (epic.NeedsMockup && !epic.IsMockupDone)
        {
            epic.SetEpicAgentInstruction("Mockup required before waterproofing. Proceeding to mockup.");

            return new MockupState();
        }

        if (epic.NeedsAgentSwarm())
        {
            epic.RaiseAgentSwarm(
                objective: $"Read the epic document at {epic.EpicDocumentPath} and reach agreement on scope and responsibilities. Do NOT begin any implementation — this is scope alignment only.",
                toStateName: Name,
                instruction: $"""
                    Agent swarm raised for waterproofing alignment.
                    Use the tmux-broker MCP tools (mcp__tmux-broker__send_message / mcp__tmux-broker__get_message) to message each coding agent — do NOT spawn sub-agents.
                    IMPORTANT: Coding agents do not have the epic-tracker MCP tool. All communication with them must go through tmux-broker messages only.
                    Ask each agent to read the epic document and reply AGREE or DISAGREE with a note on their scope. Make clear they must NOT begin implementation yet.
                    Call submit_agreement for each agent on their behalf, then call Advance.
                    """
            );

            return new AgentSwarmState();
        }

        epic.ResetAgentSwarm("All agents have reached consensus. Proceed to spec writing.");

        return new SpecWritingState();
    }
}

