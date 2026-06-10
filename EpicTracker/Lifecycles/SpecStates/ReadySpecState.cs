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

        if (TryGetBlockingDeps(spec, context.Epic.Specs, out var blockingDeps))
        {
            var blocking = string.Join(", ", blockingDeps.Select(d => $"{d.Id} ({d.CurrentStateName})"));

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

    private static bool TryGetBlockingDeps(Spec spec, IEnumerable<Spec> allSpecs, out List<Spec> blockingDeps)
    {
        var resolved = new[] { AcSpecState.StateName, DoneSpecState.StateName };

        blockingDeps = spec.DependsOn
            .Select(depId => allSpecs.FirstOrDefault(s => s.Id == depId))
            .Where(dep => dep is not null && !resolved.Contains(dep.CurrentStateName) && !resolved.Contains(dep.LastKnownStateName))
            .Select(dep => dep!)
            .ToList();

        return blockingDeps.Count > 0;
    }
}
