using EpicTracker.Contracts;
using EpicTracker.Lifecycles.EpicStates;
using Xunit;

namespace EpicTracker.Tests;

public class EpicStateTests
{
    private static Epic BaseEpic() => new()
    {
        Id = "epic-1",
        Name = "My Epic",
        EpicAgent = "epic-agent-1",
        Slug = "epic-1",
        CodingAgents = ["agent-1", "agent-2"]
    };

    private static AgentSwarm ConsensusSwarm(string toStateName, List<string> agentIds) => new()
    {
        Objective = "Test objective",
        ToStateName = toStateName,
        Agreements = agentIds.Concat(["epic-agent-1"])
            .Select(id => new AgentAgreement { AgentId = id, HasAgreed = true })
            .ToList()
    };

    // ── DraftingState ────────────────────────────────────────────────────────

    [Fact]
    public async Task DraftingState_BlocksWhenFieldsMissing()
    {
        var epic = new Epic { Id = "e", EpicAgent = "a", CodingAgents = [] };
        var state = new DraftingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("drafting", next.Name);
        Assert.NotNull(epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task DraftingState_BlocksWhenDocNotDrafted()
    {
        var epic = BaseEpic();
        var state = new DraftingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("drafting", next.Name);
        Assert.Contains("Draft the epic document", epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task DraftingState_AdvancesToWaterproofing_WhenDocDrafted()
    {
        var epic = BaseEpic();
        epic.IsDocDrafted = true;
        var state = new DraftingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("waterproofing", next.Name);
        Assert.NotNull(epic.EpicAgentInstruction);
    }

    // ── WaterproofingState ───────────────────────────────────────────────────

    [Fact]
    public async Task WaterproofingState_RoutesToMockup_WhenNeedsMockupAndNotDone()
    {
        var epic = BaseEpic();
        epic.NeedsMockup = true;
        var state = new WaterproofingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("mockup", next.Name);
        Assert.NotNull(epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task WaterproofingState_SkipsMockup_WhenAlreadyDone()
    {
        var epic = BaseEpic();
        epic.NeedsMockup = true;
        epic.IsMockupDone = true;
        var state = new WaterproofingState();

        var next = await state.MoveNext(epic);

        Assert.NotEqual("mockup", next.Name);
    }

    [Fact]
    public async Task WaterproofingState_RaisesSwarmAndRoutesToAgentSwarm_WhenNoSwarm()
    {
        var epic = BaseEpic();
        var state = new WaterproofingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("agent_swarm", next.Name);
        Assert.NotNull(epic.AgentSwarm);
        Assert.Contains("swarm", epic.EpicAgentInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WaterproofingState_ResetsSwarmAndAdvancesToSpecWriting_WhenSwarmAlreadySet()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = new AgentSwarm
        {
            Objective = "Align",
            ToStateName = "spec_writing",
            Agreements = [new AgentAgreement { AgentId = "agent-1", HasAgreed = null }]
        };
        var state = new WaterproofingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("spec_writing", next.Name);
        Assert.Null(epic.AgentSwarm);
    }

    [Fact]
    public async Task WaterproofingState_ClearsSwarmAndAdvancesToSpecWriting_WhenConsensus()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = ConsensusSwarm("spec_writing", epic.CodingAgents);
        var state = new WaterproofingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("spec_writing", next.Name);
        Assert.Null(epic.AgentSwarm);
        Assert.NotNull(epic.EpicAgentInstruction);
    }

    // ── MockupState ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MockupState_BlocksWhenNoMockupPath()
    {
        var epic = BaseEpic();
        var state = new MockupState();

        var next = await state.MoveNext(epic);

        Assert.Equal("mockup", next.Name);
        Assert.Contains("mockup folder path", epic.EpicAgentInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockupState_BlocksWhenMockupNotDone()
    {
        var epic = BaseEpic();
        epic.MockupPath = "/mockups";
        var state = new MockupState();

        var next = await state.MoveNext(epic);

        Assert.Equal("mockup", next.Name);
        Assert.Contains("mockup files", epic.EpicAgentInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockupState_RaisesHumanInLoopAndRoutesToHumanInLoopState_WhenMockupDoneAndNoReview()
    {
        var epic = BaseEpic();
        epic.MockupPath = "/mockups";
        epic.IsMockupDone = true;
        var state = new MockupState();

        var next = await state.MoveNext(epic);

        Assert.Equal("human_in_loop", next.Name);
        Assert.NotNull(epic.HumanInLoop);
        Assert.Contains("HumanInLoop", epic.EpicAgentInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockupState_ResetsAndBlocks_WhenHumanRejects()
    {
        var epic = BaseEpic();
        epic.MockupPath = "/mockups";
        epic.IsMockupDone = true;
        epic.HumanInLoop = new HumanInLoop { IsApproved = false };
        var state = new MockupState();

        var next = await state.MoveNext(epic);

        Assert.Equal("mockup", next.Name);
        Assert.False(epic.IsMockupDone);
        Assert.Null(epic.HumanInLoop);
    }

    [Fact]
    public async Task MockupState_AdvancesToWaterproofing_WhenApproved()
    {
        var epic = BaseEpic();
        epic.MockupPath = "/mockups";
        epic.IsMockupDone = true;
        epic.HumanInLoop = new HumanInLoop { IsApproved = true };
        var state = new MockupState();

        var next = await state.MoveNext(epic);

        Assert.Equal("waterproofing", next.Name);
    }

    // ── AgentSwarmState ──────────────────────────────────────────────────────

    [Fact]
    public async Task AgentSwarmState_Throws_WhenNoSwarm()
    {
        var epic = BaseEpic();
        var state = new AgentSwarmState();

        await Assert.ThrowsAsync<InvalidOperationException>(() => state.MoveNext(epic));
    }

    [Fact]
    public async Task AgentSwarmState_RoutesToTargetState_WhenConsensus()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = ConsensusSwarm("spec_writing", epic.CodingAgents);
        var state = new AgentSwarmState();

        var next = await state.MoveNext(epic);

        Assert.Equal("spec_writing", next.Name);
    }

    [Fact]
    public async Task AgentSwarmState_BlocksAndIncrementsIteration_WhenNoConsensus()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = new AgentSwarm
        {
            Objective = "Align",
            ToStateName = "spec_writing",
            Iteration = 0,
            Agreements = [new AgentAgreement { AgentId = "agent-1", HasAgreed = null }]
        };
        var state = new AgentSwarmState();

        var next = await state.MoveNext(epic);

        Assert.Equal("agent_swarm", next.Name);
        Assert.Equal(1, epic.AgentSwarm.Iteration);
        Assert.NotNull(epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task AgentSwarmState_EscalatesToHumanInLoop_WhenMaxIterationsReached()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = new AgentSwarm
        {
            Objective = "Align",
            ToStateName = "spec_writing",
            Iteration = 5,
            Agreements = [new AgentAgreement { AgentId = "agent-1", HasAgreed = false }]
        };
        var state = new AgentSwarmState();

        var next = await state.MoveNext(epic);

        Assert.Equal("human_in_loop", next.Name);
        Assert.NotNull(epic.HumanInLoop);
    }

    // ── HumanInLoopState ────────────────────────────────────────────────────

    [Fact]
    public async Task HumanInLoopState_Throws_WhenNoHumanInLoop()
    {
        var epic = BaseEpic();
        var state = new HumanInLoopState();

        await Assert.ThrowsAsync<InvalidOperationException>(() => state.MoveNext(epic));
    }

    [Fact]
    public async Task HumanInLoopState_BlocksWhenNotYetAnswered()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };
        var state = new HumanInLoopState();

        var next = await state.MoveNext(epic);

        Assert.Equal("human_in_loop", next.Name);
        Assert.Contains("Waiting for human response", epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task HumanInLoopState_RoutesToApproveState()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing",
            IsApproved = true
        };
        var state = new HumanInLoopState();

        var next = await state.MoveNext(epic);

        Assert.Equal("implementation", next.Name);
    }

    [Fact]
    public async Task HumanInLoopState_RoutesToRejectState()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing",
            IsApproved = false
        };
        var state = new HumanInLoopState();

        var next = await state.MoveNext(epic);

        Assert.Equal("spec_writing", next.Name);
    }

    // ── SpecWritingState ────────────────────────────────────────────────────

    [Fact]
    public async Task SpecWritingState_BlocksWhenNoSpecs()
    {
        var epic = BaseEpic();
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("spec_writing", next.Name);
        Assert.Contains("coding agent", epic.EpicAgentInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SpecWritingState_RaisesSwarmAndRoutesToAgentSwarm_WhenSpecsButNoSwarm()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting" });
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("agent_swarm", next.Name);
        Assert.NotNull(epic.AgentSwarm);
        Assert.Contains("swarm", epic.EpicAgentInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SpecWritingState_RaisesHumanInLoop_WhenSwarmExistsButNoConsensus()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", SpecDocPath = "/s1.md" });
        epic.AgentSwarm = new AgentSwarm
        {
            Objective = "Review specs",
            ToStateName = "spec_writing",
            Agreements = [new AgentAgreement { AgentId = "agent-1", HasAgreed = null }]
        };
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("human_in_loop", next.Name);
        Assert.NotNull(epic.HumanInLoop);
    }

    [Fact]
    public async Task SpecWritingState_RaisesHumanInLoop_WhenConsensus()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", SpecDocPath = "/s1.md" });
        epic.AgentSwarm = ConsensusSwarm("spec_writing", epic.CodingAgents);
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("human_in_loop", next.Name);
        Assert.NotNull(epic.HumanInLoop);
    }

    [Fact]
    public async Task SpecWritingState_AbandonSpecsAndReset_WhenHumanRejects()
    {
        var epic = BaseEpic();
        var spec = new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting" };
        epic.Specs.Add(spec);
        epic.AgentSwarm = ConsensusSwarm("spec_writing", epic.CodingAgents);
        epic.HumanInLoop = new HumanInLoop { IsApproved = false, ApproveToStateName = "implementation", RejectToStateName = "spec_writing" };
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("spec_writing", next.Name);
        Assert.True(spec.IsAbandoned);
        Assert.Null(epic.AgentSwarm);
        Assert.Null(epic.HumanInLoop);
    }

    [Fact]
    public async Task SpecWritingState_AdvancesToImplementation_WhenHumanApproves()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting" });
        epic.AgentSwarm = ConsensusSwarm("spec_writing", epic.CodingAgents);
        epic.HumanInLoop = new HumanInLoop { IsApproved = true, ApproveToStateName = "implementation", RejectToStateName = "spec_writing" };
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("implementation", next.Name);
    }

    [Fact]
    public async Task SpecWritingState_AllAbandoned_SetsInstructionAndReturnsThis()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", IsAbandoned = true });
        epic.Specs.Add(new Spec { Id = "s2", AssignedAgentId = "agent-2", CurrentStateName = "spec_drafting", IsAbandoned = true });
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("spec_writing", next.Name);
        Assert.Contains("coding agent", epic.EpicAgentInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SpecWritingState_SwarmConsensus_ApprovesSpecsResetsSwarmRaisesHumanInLoop()
    {
        var epic = BaseEpic();
        var spec = new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", SpecDocPath = "/s1.md" };
        epic.Specs.Add(spec);
        epic.AgentSwarm = ConsensusSwarm("spec_writing", epic.CodingAgents);
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("human_in_loop", next.Name);
        Assert.True(spec.IsSpecApproved);
        Assert.Null(epic.AgentSwarm);
        Assert.NotNull(epic.HumanInLoop);
    }

    [Fact]
    public async Task SpecWritingState_NoPendingSpecs_NoHuman_RaisesHumanInLoopAndRoutesToHumanInLoopState()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", SpecDocPath = "/s1.md", IsSpecApproved = true });
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("human_in_loop", next.Name);
        Assert.NotNull(epic.HumanInLoop);
        Assert.Equal("implementation", epic.HumanInLoop!.ApproveToStateName);
        Assert.Equal("spec_writing", epic.HumanInLoop!.RejectToStateName);
    }

    [Fact]
    public async Task SpecWritingState_NoPendingSpecs_HumanApproved_AdvancesToImplementation()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", SpecDocPath = "/s1.md", IsSpecApproved = true });
        epic.HumanInLoop = new HumanInLoop { IsApproved = true, ApproveToStateName = "implementation", RejectToStateName = "spec_writing" };
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("implementation", next.Name);
    }

    [Fact]
    public async Task SpecWritingState_NoPendingSpecs_HumanRejected_AbandonsAllSpecsResetsHumanReturnsThis()
    {
        var epic = BaseEpic();
        var spec = new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", SpecDocPath = "/s1.md", IsSpecApproved = true };
        epic.Specs.Add(spec);
        epic.HumanInLoop = new HumanInLoop { IsApproved = false, ApproveToStateName = "implementation", RejectToStateName = "spec_writing" };
        var state = new SpecWritingState();

        var next = await state.MoveNext(epic);

        Assert.Equal("spec_writing", next.Name);
        Assert.True(spec.IsAbandoned);
        Assert.Null(epic.HumanInLoop);
        Assert.NotNull(epic.EpicAgentInstruction);
    }

    // ── ImplementationState ──────────────────────────────────────────────────

    [Fact]
    public async Task ImplementationState_ApprovesAllSpecs_OnEntry()
    {
        var epic = BaseEpic();
        var spec = new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", IsSpecApproved = false };
        epic.Specs.Add(spec);
        var state = new ImplementationState();

        await state.MoveNext(epic);

        Assert.True(spec.IsSpecApproved);
    }

    [Fact]
    public async Task ImplementationState_BlocksWhileSpecsInDrafting()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "spec_drafting", SpecDocPath = "/s1.md" });
        var state = new ImplementationState();

        var next = await state.MoveNext(epic);

        Assert.Equal("implementation", next.Name);
        Assert.NotNull(epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task ImplementationState_BlocksWhileSpecsInProgress()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "coding" });
        var state = new ImplementationState();

        var next = await state.MoveNext(epic);

        Assert.Equal("implementation", next.Name);
        Assert.Contains("Ping", epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task ImplementationState_RaisesHumanInLoop_WhenAllDone()
    {
        var epic = BaseEpic();
        epic.Specs.Add(new Spec { Id = "s1", AssignedAgentId = "agent-1", CurrentStateName = "done", SpecDocPath = "/s1.md" });
        var state = new ImplementationState();

        var next = await state.MoveNext(epic);

        Assert.Equal("human_in_loop", next.Name);
        Assert.NotNull(epic.HumanInLoop);
        Assert.Equal("closed", epic.HumanInLoop!.ApproveToStateName);
        Assert.Equal("spec_writing", epic.HumanInLoop!.RejectToStateName);
    }

    // ── ClosedState ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ClosedState_IsTerminal()
    {
        var epic = BaseEpic();
        var state = new ClosedState();

        var next = await state.MoveNext(epic);

        Assert.Equal("closed", next.Name);
    }
}
