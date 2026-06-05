namespace EpicTracker.Lifecycles.EpicStates;

internal class DraftingState : EpicState
{
    public const string StateName = "drafting";

    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (!TryValidate(context.Epic, out var instruction))
        {
            return Exit(context, instruction);
        }

        if (!epic.IsDocDrafted)
        {
            return Exit(
                context: context, 
                instruction: $"""
                    Draft the epic document at {epic.EpicDocumentPath}.
                    Brief: {epic.Brief}
                    Follow the governance document at {epic.EpicGovernancePath} for the required format.
                    Once written, call update_epic(IsDocDrafted, true) then call advance({epic.Id}).
                    """);
        }
        
        return new WaterproofingState();
    }

    protected override bool UpdateEpicFieldAt(EpicContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Epic.Brief))
        {
            context.Epic.Brief = value;
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

