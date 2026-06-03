namespace EpicTracker.Lifecycles.EpicStates;

internal class MockupState : EpicState
{
    public override string Name => "mockup";

    public override async Task<EpicState> MoveNext(Epic epic, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(epic.MockupPath))
        {
            epic.SetEpicAgentInstruction("Ask the user for the mockup folder path where mockup files should be written.");
            return this;
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
