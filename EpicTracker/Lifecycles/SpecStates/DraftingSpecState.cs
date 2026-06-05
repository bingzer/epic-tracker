namespace EpicTracker.Lifecycles.SpecStates;

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
            return Exit(
                context: context,
                instruction: $"""
                    Spec {spec.Id} is not approved yet. Ask the approver to review the spec and approve it, then call advance_spec({spec.Id}).
                    """
            );
        }

        if (!context.FileSystem.FileExists(spec.SpecDocPath))
        {
            return Exit(
                context: context,
                instruction: $"""
                    Spec {spec.Id} is approved but the spec document cannot be found at {spec.SpecDocPath}.
                    Ask {spec.AssignedAgentName} to confirm the correct path, then call update_spec({spec.Id}, SpecDocPath, <corrected path>) and advance_spec({spec.Id}).
                    """
            );
        }
        
        return new ReadySpecState();
    }
}
