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
            epic.SetEpicAgentInstruction($"Call advance(\"{epic.Id}\") to continue.");
            return new WaterproofingState();
        }

        if (!epic.IsMockupDone)
        {
            epic.SetEpicAgentInstruction($"""
                Create mockup files at {epic.MockupDirectory}. Mockups can be HTML or plain text documents.
                Once complete, call update_epic with field IsMockupDone=true then call advance("{epic.Id}").
                """);

            return this;
        }

        if (epic.NeedsHumanReview())
        {
            epic.RaiseHumanInLoop(
                questions: $"Mockup is ready for review at {epic.MockupDirectory}. Please approve to proceed to waterproofing, or reject with feedback.",
                approveToStateName: WaterproofingState.StateName,
                rejectToStateName: Name,
                instruction: $"Mockup ready. HumanInLoop raised for human review. Call advance(\"{epic.Id}\") then wait for tmux to wake you."
            );

            return new HumanInLoopState();
        }

        if (epic.IsHumanRejected())
        {
            epic.IsMockupDone = false;
            epic.ResetHumanApproval($"""
                The mockup was rejected. Review the human's feedback and revise the mockup files at {epic.MockupDirectory}.
                Once revised, call update_epic with field IsMockupDone=true then call advance("{epic.Id}").
                """);

            return this;
        }

        epic.SetEpicAgentInstruction($"Mockup approved. Call advance(\"{epic.Id}\") to continue.");
        return new WaterproofingState();
    }
}
