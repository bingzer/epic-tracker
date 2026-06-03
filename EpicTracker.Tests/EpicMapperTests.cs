using EpicTracker.Contracts;
using EpicTracker.Data;
using System.Text.Json;

namespace EpicTracker.Tests;

public class EpicMapperTests
{
    // ── ToEpic ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToEpic_MapsAllScalarFields()
    {
        var entity = new EpicEntity
        {
            Id = "epic-1",
            Name = "Test Epic",
            EpicAgent = "agent-1",
            Description = "A description",
            EpicDocumentPath = "/doc.md",
            EpicGovernancePath = "/gov.md",
            NeedsMockup = true,
            IsDocDrafted = true,
            MockupPath = "/mockup",
            IsMockupDone = true,
            CodingAgents = "[\"ca-1\",\"ca-2\"]",
            CurrentStateName = "drafting",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var epic = EpicMapper.ToEpic(entity);

        Assert.Equal("epic-1", epic.Id);
        Assert.Equal("Test Epic", epic.Name);
        Assert.Equal("agent-1", epic.EpicAgent);
        Assert.Equal("A description", epic.Description);
        Assert.Equal("/doc.md", epic.EpicDocumentPath);
        Assert.Equal("/gov.md", epic.EpicGovernancePath);
        Assert.True(epic.NeedsMockup);
        Assert.True(epic.IsDocDrafted);
        Assert.Equal("/mockup", epic.MockupPath);
        Assert.True(epic.IsMockupDone);
        Assert.Equal("drafting", epic.CurrentStateName);
        Assert.Equal(new List<string> { "ca-1", "ca-2" }, epic.CodingAgents);
    }

    [Fact]
    public void ToEpic_DeserializesHumanInLoop()
    {
        var hil = new HumanInLoop
        {
            Questions = "Are you sure?",
            ApproveToStateName = "implementation",
            RejectToStateName = "spec_writing"
        };

        var entity = new EpicEntity
        {
            Id = "epic-2",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CodingAgents = "[]",

            CurrentStateName = "human_in_loop",
            HumanInLoop = JsonSerializer.Serialize(hil),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var epic = EpicMapper.ToEpic(entity);

        Assert.NotNull(epic.HumanInLoop);
        Assert.Equal("Are you sure?", epic.HumanInLoop.Questions);
        Assert.Equal("implementation", epic.HumanInLoop.ApproveToStateName);
        Assert.Equal("spec_writing", epic.HumanInLoop.RejectToStateName);
    }

    [Fact]
    public void ToEpic_DeserializesAgentSwarm()
    {
        var swarm = new AgentSwarm
        {
            Objective = "Align on scope",
            ToStateName = "spec_writing",
            Agreements =
            [
                new AgentAgreement { AgentId = "agent-a", HasAgreed = true }
            ]
        };

        var entity = new EpicEntity
        {
            Id = "epic-3",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CodingAgents = "[]",

            CurrentStateName = "waterproofing",
            AgentSwarm = JsonSerializer.Serialize(swarm),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var epic = EpicMapper.ToEpic(entity);

        Assert.NotNull(epic.AgentSwarm);
        Assert.Equal("Align on scope", epic.AgentSwarm.Objective);
        Assert.Equal("spec_writing", epic.AgentSwarm.ToStateName);
        Assert.Single(epic.AgentSwarm.Agreements);
        Assert.Equal("agent-a", epic.AgentSwarm.Agreements[0].AgentId);
        Assert.True(epic.AgentSwarm.Agreements[0].HasAgreed);
    }

    [Fact]
    public void ToEpic_MapsSpecs()
    {
        var now = DateTime.UtcNow;

        var spec1 = new SpecEntity
        {
            Id = "spec-1",
            EpicId = "epic-4",
            AssignedAgentId = "ca-1",
            CurrentStateName = "spec_drafting",
            CreatedAt = now,
            UpdatedAt = now
        };

        var spec2 = new SpecEntity
        {
            Id = "spec-2",
            EpicId = "epic-4",
            AssignedAgentId = "ca-2",
            CurrentStateName = "coding",
            CreatedAt = now,
            UpdatedAt = now
        };

        var entity = new EpicEntity
        {
            Id = "epic-4",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CodingAgents = "[]",

            CurrentStateName = "implementation",
            CreatedAt = now,
            UpdatedAt = now,
            Specs = [spec1, spec2]
        };

        var epic = EpicMapper.ToEpic(entity);

        Assert.Equal(2, epic.Specs.Count);
        Assert.Contains(epic.Specs, s => s.Id == "spec-1");
        Assert.Contains(epic.Specs, s => s.Id == "spec-2");
    }

    // ── ToSpec ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToSpec_MapsAllScalarFields()
    {
        var entity = new SpecEntity
        {
            Id = "spec-100",
            EpicId = "epic-100",
            AssignedAgentId = "ca-1",
            ReviewerAgentId = "reviewer-1",
            CodeReviewRequired = true,
            SpecDocPath = "/specs/s.md",
            IsSpecApproved = true,
            IsAbandoned = false,
            IsSpecDrafted = true,
            IsAcPassed = true,
            IsCodeDone = true,
            IsCodeReviewApproved = true,
            CurrentStateName = "done",
            EpicAgentInstruction = "You are done.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var spec = EpicMapper.ToSpec(entity);

        Assert.Equal("spec-100", spec.Id);
        Assert.Equal("epic-100", spec.EpicId);
        Assert.Equal("ca-1", spec.AssignedAgentId);
        Assert.Equal("reviewer-1", spec.ReviewerAgentId);
        Assert.True(spec.CodeReviewRequired);
        Assert.Equal("/specs/s.md", spec.SpecDocPath);
        Assert.True(spec.IsSpecApproved);
        Assert.False(spec.IsAbandoned);
        Assert.True(spec.IsSpecDrafted);
        Assert.True(spec.IsAcPassed);
        Assert.True(spec.IsCodeDone);
        Assert.True(spec.IsCodeReviewApproved);
        Assert.Equal("done", spec.CurrentStateName);
        Assert.Equal("You are done.", spec.EpicAgentInstruction);
    }

    [Fact]
    public void ToSpec_DeserializesHumanInLoop()
    {
        var hil = new HumanInLoop
        {
            Questions = "Approve this spec?",
            ApproveToStateName = "done",
            RejectToStateName = "coding"
        };

        var entity = new SpecEntity
        {
            Id = "spec-200",
            EpicId = "epic-200",
            AssignedAgentId = "ca-1",
            CurrentStateName = "spec_human_in_loop",
            HumanInLoop = JsonSerializer.Serialize(hil),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var spec = EpicMapper.ToSpec(entity);

        Assert.NotNull(spec.HumanInLoop);
        Assert.Equal("Approve this spec?", spec.HumanInLoop.Questions);
        Assert.Equal("done", spec.HumanInLoop.ApproveToStateName);
        Assert.Equal("coding", spec.HumanInLoop.RejectToStateName);
    }

    // ── SyncToEntity ─────────────────────────────────────────────────────────

    [Fact]
    public void SyncToEntity_WritesHumanInLoopJson()
    {
        var epic = new Epic
        {
            Id = "epic-x",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CurrentStateName = "human_in_loop",
            HumanInLoop = new HumanInLoop
            {
                Questions = "Proceed?",
                ApproveToStateName = "implementation",
                RejectToStateName = "spec_writing"
            }
        };

        var entity = new EpicEntity
        {
            Id = "epic-x",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CodingAgents = "[]",

            CurrentStateName = "human_in_loop",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EpicMapper.SyncToEntity(epic, entity);

        Assert.NotNull(entity.HumanInLoop);

        var deserialized = JsonSerializer.Deserialize<HumanInLoop>(entity.HumanInLoop!);

        Assert.NotNull(deserialized);
        Assert.Equal("Proceed?", deserialized.Questions);
        Assert.Equal("implementation", deserialized.ApproveToStateName);
    }

    [Fact]
    public void SyncToEntity_ClearsHumanInLoop_WhenNull()
    {
        var epic = new Epic
        {
            Id = "epic-y",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CurrentStateName = "drafting",
            HumanInLoop = null
        };

        var entity = new EpicEntity
        {
            Id = "epic-y",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CodingAgents = "[]",

            CurrentStateName = "drafting",
            HumanInLoop = "{\"Questions\":\"old\"}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EpicMapper.SyncToEntity(epic, entity);

        Assert.Null(entity.HumanInLoop);
    }

    [Fact]
    public void SyncToEntity_WritesAgentSwarmJson()
    {
        var epic = new Epic
        {
            Id = "epic-z",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CurrentStateName = "waterproofing",
            AgentSwarm = new AgentSwarm
            {
                Objective = "Agree on plan",
                ToStateName = "spec_writing",
                Agreements = [new AgentAgreement { AgentId = "ca-1" }]
            }
        };

        var entity = new EpicEntity
        {
            Id = "epic-z",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CodingAgents = "[]",

            CurrentStateName = "waterproofing",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EpicMapper.SyncToEntity(epic, entity);

        Assert.NotNull(entity.AgentSwarm);

        var deserialized = JsonSerializer.Deserialize<AgentSwarm>(entity.AgentSwarm!);

        Assert.NotNull(deserialized);
        Assert.Equal("Agree on plan", deserialized.Objective);
        Assert.Equal("spec_writing", deserialized.ToStateName);
        Assert.Single(deserialized.Agreements);
    }

    // ── SyncSpecToEntity ──────────────────────────────────────────────────────

    [Fact]
    public void SyncSpecToEntity_WritesAllFlags()
    {
        var spec = new Spec
        {
            Id = "spec-300",
            EpicId = "epic-300",
            AssignedAgentId = "ca-1",
            CurrentStateName = "ac",
            IsSpecApproved = true,
            IsAbandoned = false,
            IsSpecDrafted = true,
            IsCodeDone = true,
            IsCodeReviewApproved = true,
            IsAcPassed = false
        };

        spec.SetEpicAgentInstruction("Run the tests.");

        var entity = new SpecEntity
        {
            Id = "spec-300",
            EpicId = "epic-300",
            AssignedAgentId = "ca-1",
            CurrentStateName = "coding",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EpicMapper.SyncSpecToEntity(spec, entity);

        Assert.Equal("ac", entity.CurrentStateName);
        Assert.Equal("Run the tests.", entity.EpicAgentInstruction);
        Assert.True(entity.IsSpecApproved);
        Assert.False(entity.IsAbandoned);
        Assert.True(entity.IsSpecDrafted);
        Assert.True(entity.IsCodeDone);
        Assert.True(entity.IsCodeReviewApproved);
        Assert.False(entity.IsAcPassed);
    }

    // ── ToAudit ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToAudit_CapturesFromToStateAndInstruction()
    {
        var epic = new Epic
        {
            Id = "epic-audit",
            Name = "Epic",
            EpicAgent = "agent-1",
            EpicDocumentPath = "/d",
            EpicGovernancePath = "/g",
            CurrentStateName = "waterproofing"
        };

        epic.SetEpicAgentInstruction("Raise a swarm.");

        var audit = EpicMapper.ToAudit("epic-audit", "agent-1", "drafting", epic);

        Assert.Equal("epic-audit", audit.EpicId);
        Assert.Equal("agent-1", audit.EpicAgentId);
        Assert.Equal("drafting", audit.FromState);
        Assert.Equal("waterproofing", audit.ToState);
        Assert.Equal("Raise a swarm.", audit.EpicAgentInstruction);
    }
}
