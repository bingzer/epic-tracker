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

    // ── RaiseAgentSwarm ──────────────────────────────────────────────────────

    [Fact]
    public void RaiseAgentSwarm_CreatesSwarmWithAllCodingAgentsPlusEpicAgent()
    {
        var epic = BaseEpic();

        epic.RaiseAgentSwarm("Do the thing", "spec_writing");

        Assert.NotNull(epic.AgentSwarm);
        Assert.Equal("Do the thing", epic.AgentSwarm.Objective);
        Assert.Equal("spec_writing", epic.AgentSwarm.ToStateName);

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

        epic.RaiseAgentSwarm("Objective", "target_state");

        Assert.All(epic.AgentSwarm!.Agreements, a => Assert.Null(a.HasAgreed));
    }

    // ── ResetAgentSwarm ──────────────────────────────────────────────────────

    [Fact]
    public void ResetAgentSwarm_SetsSwarmToNull()
    {
        var epic = BaseEpic();
        epic.AgentSwarm = new AgentSwarm { Objective = "x", ToStateName = "y" };

        epic.ResetAgentSwarm();

        Assert.Null(epic.AgentSwarm);
    }

    // ── HasHumanApproved ─────────────────────────────────────────────────────

    [Fact]
    public void HasHumanApproved_ReturnsNull_WhenNoHumanInLoop()
    {
        var epic = BaseEpic();

        Assert.Null(epic.HasHumanApproved());
    }

    [Fact]
    public void HasHumanApproved_ReturnsNull_WhenHumanNotYetAnswered()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };

        Assert.Null(epic.HasHumanApproved());
    }

    [Fact]
    public void HasHumanApproved_ReturnsTrue_WhenApproved()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing",
            IsApproved = true
        };

        Assert.True(epic.HasHumanApproved());
    }

    [Fact]
    public void HasHumanApproved_ReturnsFalse_WhenRejected()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing",
            IsApproved = false
        };

        Assert.False(epic.HasHumanApproved());
    }

    // ── IsAwaitingHumanApproval ──────────────────────────────────────────────

    [Fact]
    public void IsAwaitingHumanApproval_ReturnsFalse_WhenNoHumanInLoop()
    {
        var epic = BaseEpic();

        Assert.False(epic.IsAwaitingHumanApproval());
    }

    [Fact]
    public void IsAwaitingHumanApproval_ReturnsTrue_WhenHumanInLoopSet()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };

        Assert.True(epic.IsAwaitingHumanApproval());
    }

    // ── ResetHumanApproval ───────────────────────────────────────────────────

    [Fact]
    public void ResetHumanApproval_SetsHumanInLoopToNull()
    {
        var epic = BaseEpic();
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };

        epic.ResetHumanApproval();

        Assert.Null(epic.HumanInLoop);
    }

    // ── RaiseHumanInLoop ─────────────────────────────────────────────────────

    [Fact]
    public void RaiseHumanInLoop_CreatesHumanInLoopWithCorrectValues()
    {
        var epic = BaseEpic();

        epic.RaiseHumanInLoop("Are you sure?", "implementation", "spec_writing");

        Assert.NotNull(epic.HumanInLoop);
        Assert.Equal("Are you sure?", epic.HumanInLoop.Questions);
        Assert.Equal("implementation", epic.HumanInLoop.ApproveToStateName);
        Assert.Equal("spec_writing", epic.HumanInLoop.RejectToStateName);
        Assert.Null(epic.HumanInLoop.IsApproved);
    }
}
