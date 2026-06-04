namespace EpicTracker.Lifecycles.EpicStates;

internal class DraftingState : EpicState
{
    public override string Name => "drafting";

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (!TryValidate(epic, out var instruction))
        {
            epic.SetEpicAgentInstruction(instruction);
            return this;
        }

        if (!epic.IsDocDrafted)
        {
            epic.SetEpicAgentInstruction($"""
                Draft the epic document at {epic.EpicDocumentPath}.
                Brief: {epic.Brief}
                Write a structured doc with sections: Goal, Background, Scope, Out of Scope, Open Questions.
                If anything is unclear, call raise_human_in_loop before writing.
                Once written, call update_epic(IsDocDrafted, true) then call advance.
                """);

            return this;
        }

        epic.SetEpicAgentInstruction($"""
            The epic document is drafted at {epic.EpicDocumentPath}.
            Follow governance process to write the document. Epic governance path is {epic.EpicGovernancePath}.
            Review it for completeness and clarity. If it needs work, call raise_human_in_loop with feedback.
            Once it's good, call advance.
            """);

        return new WaterproofingState();
    }

    private static bool TryValidate(Epic epic, out string instruction)
    {
        if (string.IsNullOrWhiteSpace(epic.EpicAgent))
        {
            instruction = "Missing epic agent";
            return false;
        }

        instruction = string.Empty;
        return true;
    }
}

