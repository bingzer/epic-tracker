namespace EpicTracker.Lifecycles.EpicStates;

internal class MockupState : EpicState
{
    public override string Name => "mockup";

    protected override async Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var epic = context.Epic;

        if (!epic.NeedsMockup)
        {
            return new WaterproofingState();
        }

        if (string.IsNullOrWhiteSpace(epic.MockupPath))
        {
            epic.MockupPath = Path.Combine(epic.BasePath, "epics", epic.Slug, "mockups");
        }

        if (!epic.IsMockupDone)
        {
            epic.SetEpicAgentInstruction($"""
                Create mockup files at {epic.MockupPath}. Mockups can be HTML or plain text documents.
                Once complete, call update_epic with field IsMockupDone=true and call Advance.
                {AgentSwarm.OptionalSwarmNudge}
                """);

            return this;
        }

        if (epic.NeedsHumanReview())
        {
            epic.RaiseHumanInLoop(
                questions: $"Mockup is ready for review at {epic.MockupPath}. Please approve to proceed to waterproofing, or reject with feedback.",
                approveToStateName: new WaterproofingState().Name,
                rejectToStateName: Name,
                instruction: "Mockup ready. HumanInLoop raised for human review. Wait for further instruction."
            );

            return new HumanInLoopState();
        }

        if (epic.IsHumanRejected())
        {
            epic.IsMockupDone = false;
            epic.ResetHumanApproval($"""
                The mockup was rejected. Review the human's feedback and revise the mockup files at {epic.MockupPath}.
                {AgentSwarm.OptionalSwarmNudge}
                """);

            return this;
        }

        return new WaterproofingState();
    }
}
