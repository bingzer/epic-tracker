namespace EpicTracker.Lifecycles.EpicStates;

internal class DraftingState : EpicState
{
    public const string StateName = "drafting";

    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;
        epic.LastKnownStateName = Name;

        if (!TryValidate(context.Epic, out var instruction))
        {
            return Exit(context, instruction);
        }

        if (!epic.IsBriefRefined)
        {
            return Exit(context, instruction: $"""
                Review the brief: "{epic.Brief}"

                First, assess quality: is this brief specific enough to write an epic document from?
                A brief like "stuff" or "fix things" is too vague — raise_human_in_loop to ask for more detail.

                If the brief is workable, rewrite it into a clear 2-3 sentence description that captures the intent, scope, and goals. Fix spelling and grammar. Do NOT add scope or make assumptions beyond what the user wrote.
                Call update_epic({epic.Id}, Brief, <rewritten brief>) and update_epic({epic.Id}, IsBriefRefined, true), then advance({epic.Id}).

                You may raise_human_in_loop at ANY point in the process if you need human input.
                Use approveToStateName: "{DraftingState.StateName}" and rejectToStateName: "{DraftingState.StateName}" so the process restarts from drafting after human responds.
                """);
        }

        if (!epic.IsDocDrafted)
        {
            return Exit(
                context: context,
                instruction: $"""
                    Draft the epic document at {epic.EpicDocumentPath}.
                    Brief: {epic.Brief}
                    Write a concise, high-level document capturing intent and goals only. Do NOT scan code, read files, or research the codebase.
                    Follow the governance document at {epic.EpicGovernancePath} for the required format.
                    Once written, call update_epic({epic.Id}, IsDocDrafted, true) then call advance({epic.Id}).
                    """);
        }
        
        return new WaterproofingState();
    }

    protected override bool UpdateEpicFieldAt(EpicContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Epic.Brief))
        {
            context.Epic.Brief = value;
            context.Epic.IsBriefRefined = false;
            return true;
        }

        if (fieldName == nameof(Epic.IsBriefRefined))
        {
            context.Epic.IsBriefRefined = bool.Parse(value);
            return true;
        }

        if (fieldName == nameof(Epic.NeedsMockup))
        {
            context.Epic.NeedsMockup = bool.Parse(value);
            return true;
        }

        if (fieldName == nameof(Epic.IsDocDrafted))
        {
            context.Epic.IsDocDrafted = bool.Parse(value);
            return true;
        }

        return false;
    }

    private static bool TryValidate(Epic epic, out string instruction)
    {
        if (string.IsNullOrWhiteSpace(epic.EpicAgentName))
        {
            instruction = "Missing epic agent";
            return false;
        }

        if (string.IsNullOrWhiteSpace(epic.BasePath))
        {
            instruction = "Missing base path";
            return false;
        }

        if (string.IsNullOrWhiteSpace(epic.Slug))
        {
            instruction = "Missing slug";
            return false;
        }

        if (string.IsNullOrWhiteSpace(epic.Brief))
        {
            instruction = "Missing brief — update_epic with a Brief before advancing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(epic.Name))
        {
            instruction = "Missing name — update_epic with a Name before advancing";
            return false;
        }

        if (epic.CodingAgentNames.Count == 0)
        {
            instruction = "No coding agents assigned — update_epic with at least one CodingAgent before advancing";
            return false;
        }

        instruction = string.Empty;
        return true;
    }
}

