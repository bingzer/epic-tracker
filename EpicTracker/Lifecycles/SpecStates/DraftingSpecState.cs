namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Holding state while the epic is in spec_writing. Advances to coding once the spec is approved by the epic.
/// </summary>
internal class DraftingSpecState : SpecState
{
    public const string StateName = "spec_drafting";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        if (!spec.IsSpecApproved)
        {
            return this;
        }

        if (!context.FileSystem.FileExists(spec.SpecDocPath))
        {
            spec.SetEpicAgentInstruction($"""
                Spec {spec.Id} is approved but the spec document cannot be found at {spec.SpecDocPath}.
                Ask {spec.AssignedAgentId} to confirm the correct path, then call update_spec({spec.Id}, SpecDocPath, <corrected path>) and advance_spec({spec.Id}).
                """);

            return this;
        }

        spec.SetEpicAgentInstruction($"""
            Spec {spec.Id} is approved. The spec document is confirmed at {spec.SpecDocPath}.
            Notify {spec.AssignedAgentId} that their spec is approved and they should stand by — a human must click "Code Now" in the dashboard before coding begins.
            """);

        return new ReadySpecState();
    }
}
