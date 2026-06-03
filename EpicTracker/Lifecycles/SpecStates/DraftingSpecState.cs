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
            Send {spec.AssignedAgentId} the spec at {spec.SpecDocPath} and tell them to implement it and report back when done.
            Once they confirm, call update_spec({spec.Id}, IsCodeDone, true) and then advance_spec({spec.Id}) on their behalf.
            """);

        return new CodingSpecState();
    }
}
