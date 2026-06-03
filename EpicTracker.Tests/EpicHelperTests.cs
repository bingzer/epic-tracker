using EpicTracker.Contracts;
using Xunit;

namespace EpicTracker.Tests;

public class EpicHelperTests
{
    private static Epic BaseEpic() => new()
    {
        Id = "epic-1",
        Name = "My Epic",
        EpicAgent = "epic-agent-1",
        Slug = "epic-1",
        CodingAgents = ["agent-1", "agent-2"]
    };

    // ── AgentSwarmHasConsensus ───────────────────────────────────────────────

    [Fact]
    public void AgentSwarmHasConsensus_ReturnsFalse_WhenNoSwarm()
    {
        var epic = BaseEpic();

        Assert.False(epic.AgentSwarmHasConsensus());
    }

    [Fact]
    public void AgentSwarmHasConsensus_ReturnsFalse_WhenSwarmHasNoConsensus()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = new AgentSwarm
        {
            Objective = "Review",
            ToStateName = "spec_writing",
            Agreements =
            [
                new AgentAgreement { AgentId = "agent-1", HasAgreed = true },
                new AgentAgreement { AgentId = "agent-2", HasAgreed = null }
            ]
        };

        Assert.False(epic.AgentSwarmHasConsensus());
    }

    [Fact]
    public void AgentSwarmHasConsensus_ReturnsTrue_WhenAllAgreed()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = new AgentSwarm
        {
            Objective = "Review",
            ToStateName = "spec_writing",
            Agreements =
            [
                new AgentAgreement { AgentId = "agent-1", HasAgreed = true },
                new AgentAgreement { AgentId = "agent-2", HasAgreed = true }
            ]
        };

        Assert.True(epic.AgentSwarmHasConsensus());
    }

    // ── NeedsAgentSwarm ──────────────────────────────────────────────────────

    [Fact]
    public void NeedsAgentSwarm_ReturnsTrue_WhenNoSwarm()
    {
        var epic = BaseEpic();

        Assert.True(epic.NeedsAgentSwarm());
    }

    [Fact]
    public void NeedsAgentSwarm_ReturnsFalse_WhenSwarmSet()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = new AgentSwarm { Objective = "x", ToStateName = "y" };

        Assert.False(epic.NeedsAgentSwarm());
    }

    // ── RaiseAgentSwarm ──────────────────────────────────────────────────────

    [Fact]
    public void RaiseAgentSwarm_CreatesSwarmWithAllCodingAgentsPlusEpicAgent()
    {
        var epic = BaseEpic();

        epic.RaiseAgentSwarm(
            objective: "Do the thing",
            toStateName: "spec_writing",
            instruction: "Go do it."
        );

        Assert.NotNull(epic.AgentSwarm);
        Assert.Equal("Do the thing", epic.AgentSwarm.Objective);
        Assert.Equal("spec_writing", epic.AgentSwarm.ToStateName);
        Assert.Equal("Go do it.", epic.EpicAgentInstruction);

        var participantIds = epic.AgentSwarm.Agreements.Select(a => a.AgentId).ToList();
        Assert.Contains("agent-1", participantIds);
        Assert.Contains("agent-2", participantIds);
        Assert.Contains("epic-agent-1", participantIds);
        Assert.Equal(3, participantIds.Count);
    }

    [Fact]
    public void RaiseAgentSwarm_SetsAgreementsWithNoDecisionYet()
    {
        var epic = BaseEpic();

        epic.RaiseAgentSwarm(objective: "Objective", toStateName: "target_state", instruction: "Go.");

        Assert.All(epic.AgentSwarm!.Agreements, a => Assert.Null(a.HasAgreed));
    }

    // ── ResetAgentSwarm ──────────────────────────────────────────────────────

    [Fact]
    public void ResetAgentSwarm_SetsSwarmToNullAndSetsInstruction()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = new AgentSwarm { Objective = "x", ToStateName = "y" };

        epic.ResetAgentSwarm("Swarm cleared.");

        Assert.Null(epic.AgentSwarm);
        Assert.Equal("Swarm cleared.", epic.EpicAgentInstruction);
    }

    // ── NeedsHumanReview ─────────────────────────────────────────────────────

    [Fact]
    public void NeedsHumanReview_ReturnsTrue_WhenNoHumanInLoop()
    {
        var epic = BaseEpic();

        Assert.True(epic.NeedsHumanReview());
    }

    [Fact]
    public void NeedsHumanReview_ReturnsFalse_WhenHumanInLoopSet()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };

        Assert.False(epic.NeedsHumanReview());
    }

    // ── IsHumanRejected ──────────────────────────────────────────────────────

    [Fact]
    public void IsHumanRejected_ReturnsFalse_WhenNoHumanInLoop()
    {
        var epic = BaseEpic();

        Assert.False(epic.IsHumanRejected());
    }

    [Fact]
    public void IsHumanRejected_ReturnsFalse_WhenHumanNotYetAnswered()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };

        Assert.False(epic.IsHumanRejected());
    }

    [Fact]
    public void IsHumanRejected_ReturnsTrue_WhenRejected()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing",
            IsApproved = false
        };

        Assert.True(epic.IsHumanRejected());
    }

    [Fact]
    public void IsHumanRejected_ReturnsFalse_WhenApproved()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing",
            IsApproved = true
        };

        Assert.False(epic.IsHumanRejected());
    }

    // ── IsHumanApproved ──────────────────────────────────────────────────────

    [Fact]
    public void IsHumanApproved_ReturnsFalse_WhenNoHumanInLoop()
    {
        var epic = BaseEpic();

        Assert.False(epic.IsHumanApproved());
    }

    [Fact]
    public void IsHumanApproved_ReturnsFalse_WhenHumanNotYetAnswered()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };

        Assert.False(epic.IsHumanApproved());
    }

    [Fact]
    public void IsHumanApproved_ReturnsTrue_WhenApproved()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing",
            IsApproved = true
        };

        Assert.True(epic.IsHumanApproved());
    }

    [Fact]
    public void IsHumanApproved_ReturnsFalse_WhenRejected()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing",
            IsApproved = false
        };

        Assert.False(epic.IsHumanApproved());
    }

    // ── ResetHumanApproval ───────────────────────────────────────────────────

    [Fact]
    public void ResetHumanApproval_SetsHumanInLoopToNullAndSetsInstruction()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };

        epic.ResetHumanApproval("Human review cleared.");

        Assert.Null(epic.HumanInLoop);
        Assert.Equal("Human review cleared.", epic.EpicAgentInstruction);
    }

    // ── RaiseHumanInLoop ─────────────────────────────────────────────────────

    [Fact]
    public void RaiseHumanInLoop_CreatesHumanInLoopWithCorrectValues()
    {
        var epic = BaseEpic();

        epic.RaiseHumanInLoop(
            questions: "Are you sure?",
            approveToStateName: "implementation",
            rejectToStateName: "spec_writing",
            instruction: "Waiting for human."
        );

        Assert.NotNull(epic.HumanInLoop);
        Assert.Equal("Are you sure?", epic.HumanInLoop.Questions);
        Assert.Equal("implementation", epic.HumanInLoop.ApproveToStateName);
        Assert.Equal("spec_writing", epic.HumanInLoop.RejectToStateName);
        Assert.Null(epic.HumanInLoop.IsApproved);
        Assert.Equal("Waiting for human.", epic.EpicAgentInstruction);
    }
}
