namespace EpicTracker.Lifecycles.EpicStates;

internal class MockupState : EpicState
{
    public const string StateName = "mockup";
    public override string Name => StateName;

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (!epic.NeedsMockup)
        {
            return new WaterproofingState();
        }

        if (!epic.IsMockupDone)
        {
            return Exit(
                context: context, 
                instruction: $"""
                    Create mockup files at {epic.MockupDirectory}. Mockups can be HTML or plain text documents.
                    Once complete, call update_epic with field IsMockupDone=true then call advance("{epic.Id}").
                    """
            );
        }

        if (epic.NeedsHumanReview())
        {
            return RaiseHumanInLoop(
                context: context,
                questions: $"""
                    Mockup is ready for review at {epic.MockupDirectory}. Please approve to proceed to waterproofing, or reject with feedback.
                    """,
                approveToStateName: WaterproofingState.StateName,
                rejectToStateName: Name,
                instruction: $"""
                    Mockup ready. HumanInLoop raised for human review. Call advance("{epic.Id}") then wait for tmux to wake you.
                    """
            );
        }

        if (epic.IsHumanRejected())
        {
            epic.IsMockupDone = false;
            epic.ResetHumanApproval();

            return Exit(
                context: context, 
                instruction: $"""
                    The mockup was rejected. Review the human's feedback and revise the mockup files at {epic.MockupDirectory}.
                    Once revised, call update_epic with field IsMockupDone=true then call advance("{epic.Id}").
                    """);
        }
        
        return new WaterproofingState();
    }

    protected override bool UpdateEpicFieldAt(EpicContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Epic.IsMockupDone))
        {
            context.Epic.IsMockupDone = bool.Parse(value);
            return true;
        }

        return false;
    }
}
