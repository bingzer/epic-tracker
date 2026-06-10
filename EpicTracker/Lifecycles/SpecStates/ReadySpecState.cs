namespace EpicTracker.Lifecycles.SpecStates;

internal class ReadySpecState : SpecState
{
    public const string StateName = "ready";
    public override string Name => StateName;

    protected override async Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var spec = context.Spec;

        spec.LastKnownStateName = Name;

        var unmetDeps = spec.DependsOn
            .Select(depId => context.Epic.Specs.FirstOrDefault(s => s.Id == depId))
            .Where(dep => dep is not null && dep.CurrentStateName != AcSpecState.StateName && dep.CurrentStateName != DoneSpecState.StateName)
            .Select(dep => dep!)
            .ToList();

        if (unmetDeps.Count > 0)
        {
            var blocking = string.Join(", ", unmetDeps.Select(d => $"{d.Id} ({d.CurrentStateName})"));

            return Exit(
                context: context,
                instruction: $"""
                    Spec {spec.Id} is blocked by unmet dependencies: {blocking}.
                    Wait until all dependencies reach 'ac' or 'done' before proceeding.
                    Governance: {context.Epic.EpicGovernancePath}
                    """);
        }

        if (!spec.IsReadyToCode)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Spec {spec.Id} is now in 'ready' state — waiting for a human to click "Code Now" in the dashboard.
                    Do NOT message the coding agent to begin coding yet.
                    Remind the coding agent assigned to this spec not to begin coding until you explicitly tell them to.
                    Governance: {context.Epic.EpicGovernancePath}
                    """);
        }

        return new CodingSpecState();
    }
}
