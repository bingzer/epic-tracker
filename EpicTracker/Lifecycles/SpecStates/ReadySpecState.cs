namespace EpicTracker.Lifecycles.SpecStates;

internal class ReadySpecState : SpecState
{
    public const string StateName = "ready";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        if (!spec.IsReadyToCode)
        {
            spec.SetEpicAgentInstruction($"""
                Spec {spec.Id} is ready for coding. A human must click "Code Now" in the dashboard to release it.
                Do not start coding. Wait for the human gate.
                """);

            return this;
        }

        spec.SetEpicAgentInstruction($"""
            Spec {spec.Id} has been released by the human. Hand off to coding agent {spec.AssignedAgentId}.
            """);

        return new CodingSpecState();
    }
}
