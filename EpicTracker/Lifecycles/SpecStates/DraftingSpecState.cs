namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Holding state while the epic is in spec_writing. Advances to coding once the spec is approved by the epic.
/// </summary>
internal class DraftingSpecState : SpecState
{
    public override string Name => "spec_drafting";

    public override async Task<SpecState> MoveNext(Spec spec, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (!spec.IsSpecApproved)
        {
            return this;
        }

        spec.SetEpicAgentInstruction($"""
            Hand off spec {spec.Id} to coding agent {spec.AssignedAgentId} to implement the spec at {spec.SpecDocPath}.
            Tell {spec.AssignedAgentId} to report back to you when done, then call UpdateSpec to set IsCodeDone = true and call AdvanceSpec.
            """);

        return new CodingSpecState();
    }
}
