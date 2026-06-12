namespace EpicTracker.Lifecycles.EpicStates;

/// <summary>
/// Terminal state. Epic is complete and closed.
/// </summary>
internal class ClosedState : EpicState
{
    public const string StateName = "closed";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        if (!context.FileSystem.FileExists(context.Epic.EpicDeliverablesPath))
        {
            return Exit(
                context: context,
                instruction: $"""
                    Before closing, you must write a deliverables document.
                    1. Message each coding agent via tmux asking them to summarize: what they built, which files changed (absolute paths), and how to verify their work.
                    2. Collect their replies.
                    3. Write {context.Epic.EpicDeliverablesPath} with one section per spec, following the Deliverables format in {context.Epic.EpicGovernancePath}.
                    4. Call advance("{context.Epic.Id}") once the file is written.
                    """
            );
        }

        context.Epic.LastKnownStateName = Name;

        await context.Broker.DeleteChannel($"epic-{context.Epic.Id}", context.Epic.EpicAgentName, cancellationToken);

        return Exit(
            context: context,
            instruction: "This epic is already closed."
        );
    }
}
