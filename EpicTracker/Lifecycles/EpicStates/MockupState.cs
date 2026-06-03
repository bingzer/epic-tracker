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

        if (!epic.IsAwaitingHumanApproval() || epic.HasHumanApproved() is null)
        {
            epic.SetEpicAgentInstruction("Mockup is ready for human review. Raise a HumanInLoop with the mockup location and ask the user to approve or reject.");
            return this;
        }

        if (epic.HasHumanApproved() == false)
        {
            epic.IsMockupDone = false;
            epic.ResetHumanApproval();

            epic.SetEpicAgentInstruction($"""
                The mockup was rejected. Review the human's feedback and revise the mockup files at {epic.MockupPath}.
                {AgentSwarm.OptionalSwarmNudge}
                """);

            return this;
        }

        epic.SetEpicAgentInstruction("Mockup approved. Proceeding to waterproofing.");

        return new WaterproofingState();
    }
}
