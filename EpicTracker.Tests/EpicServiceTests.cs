using EpicTracker.Contracts;
using EpicTracker.Data;
using EpicTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EpicTracker.Tests;

public class EpicServiceTests : IDisposable
{
    private readonly EpicTrackerDbContext _db;
    private readonly EpicService _svc;

    public EpicServiceTests()
    {
        var options = new DbContextOptionsBuilder<EpicTrackerDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new EpicTrackerDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _svc = new EpicService(_db, new TmuxService(NullLogger<TmuxService>.Instance));
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private CreateEpicRequest BaseEpic() => new(
        EpicAgent: "epic-agent-1",
        Brief: "Test epic",
        Name: "My Epic",
        CodingAgents: ["coding-agent-1", "coding-agent-2"],
        NeedsMockup: false,
        ReviewerAgentId: null);

    private async Task<Epic> Create() => await _svc.CreateEpic(BaseEpic());

    private async Task<Epic> Advance(string epicId) =>
        await _svc.Advance(epicId, new AdvanceEpicRequest("epic-agent-1"));

    // ── invariant: every Advance returns an EpicAgentInstruction ─────────────

    [Fact]
    public async Task Advance_AlwaysReturnsInstruction_InDraftingState()
    {
        var epic = await Create();

        var result = await Advance(epic.Id);

        Assert.NotNull(result.EpicAgentInstruction);
        Assert.NotEmpty(result.EpicAgentInstruction);
    }

    // ── drafting state ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEpic_StartsAtDrafting()
    {
        var epic = await Create();

        Assert.Equal("drafting", epic.CurrentStateName);
    }

    [Fact]
    public async Task DraftingState_BlocksWhenDocNotDrafted()
    {
        var epic = await Create();

        var result = await Advance(epic.Id);

        Assert.Equal("drafting", result.CurrentStateName);
    }

    [Fact]
    public async Task DraftingState_AdvancesToWaterproofing_WhenDocDrafted()
    {
        var epic = await Create();
        await _svc.UpdateEpicField(epic.Id, "IsDocDrafted", "true");

        var result = await Advance(epic.Id);

        Assert.Equal("waterproofing", result.CurrentStateName);
    }

    // ── waterproofing state ──────────────────────────────────────────────────

    [Fact]
    public async Task WaterproofingState_RaisesSwarmAndTransitionsToAgentSwarm_OnFirstAdvance()
    {
        var epic = await Create();
        await _svc.UpdateEpicField(epic.Id, "IsDocDrafted", "true");
        await Advance(epic.Id);

        var result = await Advance(epic.Id);

        Assert.Equal("agent_swarm", result.CurrentStateName);
        Assert.NotNull(result.EpicAgentInstruction);
    }

    [Fact]
    public async Task WaterproofingState_AdvancesToSpecWriting_WhenConsensusReached()
    {
        var epic = await Create();
        await _svc.UpdateEpicField(epic.Id, "IsDocDrafted", "true");
        await Advance(epic.Id);

        await _svc.RaiseAgentSwarm(epic.Id, new RaiseAgentSwarmRequest("Align on scope", "spec_writing"));

        var entity = await _db.FindEpicOrThrow(epic.Id);
        var swarmJson = entity.AgentSwarm!;
        var swarm = System.Text.Json.JsonSerializer.Deserialize<AgentSwarm>(swarmJson)!;

        foreach (var a in swarm.Agreements)
        {
            await _svc.SubmitAgreement(epic.Id, new SubmitAgreementRequest(a.AgentId, true, null));
        }

        var result = await Advance(epic.Id);

        Assert.Equal("spec_writing", result.CurrentStateName);
    }

    // ── human_in_loop state ──────────────────────────────────────────────────

    [Fact]
    public async Task HumanInLoopState_BlocksWhenNoResponse()
    {
        var epic = await AdvanceEpicToHumanInLoop();

        var result = await Advance(epic.Id);

        Assert.Equal("human_in_loop", result.CurrentStateName);
        Assert.Contains("Waiting for human response", result.EpicAgentInstruction);
    }

    [Fact]
    public async Task HumanInLoopState_RoutesToApproveState_WhenApproved()
    {
        var epic = await AdvanceEpicToHumanInLoop();

        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(true, null));

        var result = await Advance(epic.Id);

        Assert.Equal("implementation", result.CurrentStateName);
    }

    [Fact]
    public async Task HumanInLoopState_RoutesToRejectState_WhenRejected()
    {
        var epic = await AdvanceEpicToHumanInLoop();

        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(false, null));

        var result = await Advance(epic.Id);

        Assert.Equal("spec_writing", result.CurrentStateName);
    }

    // ── spec_writing state ───────────────────────────────────────────────────

    [Fact]
    public async Task SpecWritingState_BlocksWhenNoSpecsSubmitted()
    {
        var epic = await AdvanceEpicToSpecWriting();

        var result = await Advance(epic.Id);

        Assert.Equal("spec_writing", result.CurrentStateName);
    }

    [Fact]
    public async Task SpecWritingState_RaisesSwarmAndTransitionsToAgentSwarm_WhenSpecsSubmittedButNoSwarm()
    {
        var epic = await AdvanceEpicToSpecWriting();
        await _svc.CreateSpec(epic.Id, new CreateSpecRequest("coding-agent-1", "/specs/spec-1.md", false, null));

        var result = await Advance(epic.Id);

        Assert.Equal("agent_swarm", result.CurrentStateName);
        Assert.Contains("swarm", result.EpicAgentInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SpecWritingState_RaisesHumanInLoop_WhenConsensusReached()
    {
        var epic = await AdvanceEpicToSpecWriting();
        await _svc.CreateSpec(epic.Id, new CreateSpecRequest("coding-agent-1", "/specs/spec-1.md", false, null));

        await _svc.RaiseAgentSwarm(epic.Id, new RaiseAgentSwarmRequest("Review spec list", "spec_writing"));
        var entity = await _db.FindEpicOrThrow(epic.Id);
        var swarm = System.Text.Json.JsonSerializer.Deserialize<AgentSwarm>(entity.AgentSwarm!)!;
        foreach (var a in swarm.Agreements)
        {
            await _svc.SubmitAgreement(epic.Id, new SubmitAgreementRequest(a.AgentId, true, null));
        }

        var result = await Advance(epic.Id);

        Assert.Equal("human_in_loop", result.CurrentStateName);
    }

    [Fact]
    public async Task SpecWritingState_Rejection_AbandonsSpecsAndResets()
    {
        var epic = await AdvanceEpicToSpecWriting();
        await _svc.CreateSpec(epic.Id, new CreateSpecRequest("coding-agent-1", "/specs/spec-1.md", false, null));

        await _svc.RaiseAgentSwarm(epic.Id, new RaiseAgentSwarmRequest("Review spec list", "spec_writing"));
        var entity = await _db.FindEpicOrThrow(epic.Id);
        var swarm = System.Text.Json.JsonSerializer.Deserialize<AgentSwarm>(entity.AgentSwarm!)!;
        foreach (var a in swarm.Agreements)
        {
            await _svc.SubmitAgreement(epic.Id, new SubmitAgreementRequest(a.AgentId, true, null));
        }
        await Advance(epic.Id);

        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(false, null));
        await Advance(epic.Id);
        await Advance(epic.Id);

        ClearTracker();
        var finalEpic = await _svc.GetEpic(epic.Id);
        Assert.Equal("spec_writing", finalEpic.CurrentStateName);
        Assert.Empty(finalEpic.Specs);

        var abandonedCount = await _db.Specs.CountAsync(s => s.EpicId == epic.Id && s.IsAbandoned);
        Assert.Equal(1, abandonedCount);
    }

    // ── implementation state ─────────────────────────────────────────────────

    [Fact]
    public async Task ImplementationState_SetsIsSpecApprovedOnAllSpecs()
    {
        var epic = await AdvanceEpicToImplementation();

        await Advance(epic.Id);

        var specs = await _db.Specs.Where(s => s.EpicId == epic.Id && !s.IsAbandoned).ToListAsync();
        Assert.All(specs, s => Assert.True(s.IsSpecApproved));
    }

    [Fact]
    public async Task ImplementationState_BlocksWhileSpecsInDrafting()
    {
        var epic = await AdvanceEpicToImplementation();

        var result = await Advance(epic.Id);

        Assert.Equal("implementation", result.CurrentStateName);
        Assert.NotNull(result.EpicAgentInstruction);
    }

    [Fact]
    public async Task ImplementationState_RaisesHumanInLoop_WhenAllSpecsDone()
    {
        var epic = await AdvanceEpicToImplementation();
        await Advance(epic.Id);

        var specs = await _db.Specs.Where(s => s.EpicId == epic.Id && !s.IsAbandoned).ToListAsync();
        foreach (var s in specs)
        {
            s.CurrentStateName = "done";
            s.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var result = await Advance(epic.Id);

        Assert.Equal("human_in_loop", result.CurrentStateName);
    }

    [Fact]
    public async Task ImplementationState_AdvancesToClosed_WhenHumanApproves()
    {
        var epic = await AdvanceEpicToImplementation();
        await Advance(epic.Id);

        var specs = await _db.Specs.Where(s => s.EpicId == epic.Id && !s.IsAbandoned).ToListAsync();
        foreach (var s in specs)
        {
            s.CurrentStateName = "done";
            s.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await Advance(epic.Id);

        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(true, null));
        var result = await Advance(epic.Id);

        Assert.Equal("closed", result.CurrentStateName);
    }

    // ── mockup flow ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MockupFlow_RoutesToMockup_WhenNeedsMockup()
    {
        var epic = await _svc.CreateEpic(new CreateEpicRequest(
            EpicAgent: "epic-agent-1",
            Brief: "Mockup test",
            Name: "Mockup Epic",
            CodingAgents: ["coding-agent-1"],
            NeedsMockup: true,
            ReviewerAgentId: null));

        await _svc.UpdateEpicField(epic.Id, "IsDocDrafted", "true");

        await _svc.Advance(epic.Id, new AdvanceEpicRequest("epic-agent-1"));
        var result = await _svc.Advance(epic.Id, new AdvanceEpicRequest("epic-agent-1"));

        Assert.Equal("mockup", result.CurrentStateName);
    }

    [Fact]
    public async Task MockupFlow_AdvancesToWaterproofing_AfterApproval()
    {
        var epic = await _svc.CreateEpic(new CreateEpicRequest(
            EpicAgent: "epic-agent-1",
            Brief: "Mockup test",
            Name: "Mockup Epic",
            CodingAgents: ["coding-agent-1"],
            NeedsMockup: true,
            ReviewerAgentId: null));

        await _svc.UpdateEpicField(epic.Id, "IsDocDrafted", "true");

        await _svc.Advance(epic.Id, new AdvanceEpicRequest("epic-agent-1"));
        await _svc.Advance(epic.Id, new AdvanceEpicRequest("epic-agent-1"));

        await _svc.UpdateEpicField(epic.Id, "MockupPath", "/mockups");
        await _svc.UpdateEpicField(epic.Id, "IsMockupDone", "true");

        await _svc.Advance(epic.Id, new AdvanceEpicRequest("epic-agent-1"));

        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(true, null));
        var result = await _svc.Advance(epic.Id, new AdvanceEpicRequest("epic-agent-1"));

        Assert.Equal("waterproofing", result.CurrentStateName);
    }

    // ── agent swarm max iterations ───────────────────────────────────────────

    [Fact]
    public async Task AgentSwarm_EscalatesToHumanInLoop_AfterMaxIterations()
    {
        var epic = await Create();
        await _svc.UpdateEpicField(epic.Id, "IsDocDrafted", "true");
        await Advance(epic.Id);

        for (var i = 0; i < 7; i++)
        {
            await Advance(epic.Id);
        }

        var result = await _svc.GetEpic(epic.Id);
        Assert.Equal("human_in_loop", result.CurrentStateName);
        Assert.NotNull(result.HumanInLoop);
    }

    // ── wrong agent calling Advance ──────────────────────────────────────────

    [Fact]
    public async Task Advance_Throws_WhenWrongAgent()
    {
        var epic = await Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.Advance(epic.Id, new AdvanceEpicRequest("wrong-agent")));
    }

    // ── closed epic is terminal ──────────────────────────────────────────────

    [Fact]
    public async Task ClosedEpic_StaysClosedOnAdvance()
    {
        var epic = await AdvanceEpicToImplementation();
        await Advance(epic.Id);

        var specs = await _db.Specs.Where(s => s.EpicId == epic.Id && !s.IsAbandoned).ToListAsync();
        foreach (var s in specs)
        {
            s.CurrentStateName = "done";
            s.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await Advance(epic.Id);
        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(true, null));
        await Advance(epic.Id);
        await Advance(epic.Id);

        var result = await _svc.GetEpic(epic.Id);
        Assert.Equal("closed", result.CurrentStateName);
    }

    // ── multiple specs in implementation ─────────────────────────────────────

    [Fact]
    public async Task ImplementationState_BlocksWhileOneSpecStillInProgress()
    {
        var epic = await AdvanceEpicToImplementation();
        await _svc.CreateSpec(epic.Id, new CreateSpecRequest("coding-agent-2", "/specs/spec-2.md", false, null));

        await Advance(epic.Id);

        var specs = await _db.Specs.Where(s => s.EpicId == epic.Id && !s.IsAbandoned).ToListAsync();
        specs[0].CurrentStateName = "done";
        specs[0].UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var result = await Advance(epic.Id);

        Assert.Equal("implementation", result.CurrentStateName);
    }

    [Fact]
    public async Task ImplementationState_AdvancesToHumanInLoop_WhenAllSpecsDone()
    {
        var epic = await AdvanceEpicToImplementation();
        await _svc.CreateSpec(epic.Id, new CreateSpecRequest("coding-agent-2", "/specs/spec-2.md", false, null));

        await Advance(epic.Id);

        var specs = await _db.Specs.Where(s => s.EpicId == epic.Id && !s.IsAbandoned).ToListAsync();
        foreach (var s in specs)
        {
            s.CurrentStateName = "done";
            s.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        var result = await Advance(epic.Id);

        Assert.Equal("human_in_loop", result.CurrentStateName);
    }

    // ── UpdateSpec persists flags ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateSpec_PersistsFlags()
    {
        await SeedEpicEntity("epic-flags");
        var spec = await _svc.CreateSpec("epic-flags", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _svc.UpdateSpec(new Spec
        {
            Id = spec.Id,
            EpicId = spec.EpicId,
            AssignedAgentId = spec.AssignedAgentId,
            SpecDocPath = "/specs/updated.md",
            IsCodeDone = true,
            IsSpecDrafted = true,
            CodeReviewRequired = false
        });

        ClearTracker();
        var reloaded = await _svc.GetSpec(spec.Id);

        Assert.True(reloaded.IsCodeDone);
        Assert.True(reloaded.IsSpecDrafted);
        Assert.Equal("/specs/updated.md", reloaded.SpecDocPath);
    }

    // ── CreateSpec on non-existent epic ──────────────────────────────────────

    [Fact]
    public async Task CreateSpec_Throws_WhenEpicNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.CreateSpec("non-existent-epic", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null)));
    }

    // ── done spec is terminal ─────────────────────────────────────────────────

    [Fact]
    public async Task AdvanceSpec_DoneSpec_StaysDone()
    {
        await SeedEpicEntity("epic-done");
        var spec = await _svc.CreateSpec("epic-done", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CurrentStateName, "done"));
        ClearTracker();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("done", result.CurrentStateName);
    }

    // ── spec state machine ───────────────────────────────────────────────────

    [Fact]
    public async Task AdvanceSpec_DraftingState_BlocksUntilApproved()
    {
        await SeedEpicEntity("epic-1");
        var spec = await _svc.CreateSpec("epic-1", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("spec_drafting", result.CurrentStateName);
    }

    [Fact]
    public async Task AdvanceSpec_DraftingState_AdvancesToCoding_WhenApproved()
    {
        await SeedEpicEntity("epic-2");
        var spec = await _svc.CreateSpec("epic-2", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs
            .Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsSpecApproved, true));
        _db.ChangeTracker.Clear();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("coding", result.CurrentStateName);
        Assert.NotNull(result.EpicAgentInstruction);
    }

    [Fact]
    public async Task AdvanceSpec_CodingState_BlocksUntilCodeDone()
    {
        await SeedEpicEntity("epic-3");
        var spec = await _svc.CreateSpec("epic-3", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.CurrentStateName, "coding"));
        ClearTracker();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("coding", result.CurrentStateName);
    }

    [Fact]
    public async Task AdvanceSpec_CodingState_AdvancesToAc_WhenCodeDone_NoReview()
    {
        await SeedEpicEntity("epic-4");
        var spec = await _svc.CreateSpec("epic-4", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.CurrentStateName, "coding"));
        ClearTracker();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("ac", result.CurrentStateName);
    }

    [Fact]
    public async Task AdvanceSpec_CodingState_AdvancesToCodeReview_WhenCodeDone_ReviewRequired()
    {
        await SeedEpicEntity("epic-5");
        var spec = await _svc.CreateSpec("epic-5", new CreateSpecRequest("coding-agent-1", "/specs/s.md", true, "reviewer-1"));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.CurrentStateName, "coding"));
        ClearTracker();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("code_review", result.CurrentStateName);
    }

    [Fact]
    public async Task AdvanceSpec_CodeReview_Rejection_ResetsToCodin()
    {
        await SeedEpicEntity("epic-6");
        var spec = await _svc.CreateSpec("epic-6", new CreateSpecRequest("coding-agent-1", "/specs/s.md", true, "reviewer-1"));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.IsCodeReviewApproved, (bool?)false)
                .SetProperty(x => x.CurrentStateName, "code_review"));
        ClearTracker();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("coding", result.CurrentStateName);
        Assert.False(result.IsCodeDone);
    }

    [Fact]
    public async Task AdvanceSpec_AcState_BlocksUntilAcPassed()
    {
        await SeedEpicEntity("epic-7");
        var spec = await _svc.CreateSpec("epic-7", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.CurrentStateName, "ac"));
        ClearTracker();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("ac", result.CurrentStateName);
    }

    [Fact]
    public async Task AdvanceSpec_AcState_Failure_ResetsToCodin()
    {
        await SeedEpicEntity("epic-8");
        var spec = await _svc.CreateSpec("epic-8", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.IsAcPassed, (bool?)false)
                .SetProperty(x => x.CurrentStateName, "ac"));
        ClearTracker();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("coding", result.CurrentStateName);
        Assert.False(result.IsCodeDone);
    }

    [Fact]
    public async Task AdvanceSpec_AcState_RaisesHumanInLoop_WhenAcPassed()
    {
        await SeedEpicEntity("epic-9");
        var spec = await _svc.CreateSpec("epic-9", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.IsAcPassed, (bool?)true)
                .SetProperty(x => x.CurrentStateName, "ac"));
        ClearTracker();

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("spec_human_in_loop", result.CurrentStateName);
        Assert.NotNull(result.HumanInLoop);
    }

    [Fact]
    public async Task AdvanceSpec_HumanInLoop_BlocksUntilResponse()
    {
        await SeedEpicEntity("epic-10");
        var spec = await _svc.CreateSpec("epic-10", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.IsAcPassed, (bool?)true)
                .SetProperty(x => x.CurrentStateName, "ac"));
        ClearTracker();

        await _svc.AdvanceSpec(spec.Id);

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("spec_human_in_loop", result.CurrentStateName);
        Assert.Contains("Waiting for human response", result.EpicAgentInstruction);
    }

    [Fact]
    public async Task AdvanceSpec_HumanInLoop_Approve_AdvancesToDone()
    {
        await SeedEpicEntity("epic-11");
        var spec = await _svc.CreateSpec("epic-11", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.IsAcPassed, (bool?)true)
                .SetProperty(x => x.CurrentStateName, "ac"));
        ClearTracker();

        await _svc.AdvanceSpec(spec.Id);

        await _svc.ApproveSpecHumanInLoop(spec.Id, new ApproveSpecHumanInLoopRequest(true, null));

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("done", result.CurrentStateName);
    }

    [Fact]
    public async Task AdvanceSpec_HumanInLoop_Reject_ReturnsToCodin()
    {
        await SeedEpicEntity("epic-12");
        var spec = await _svc.CreateSpec("epic-12", new CreateSpecRequest("coding-agent-1", "/specs/s.md", false, null));

        await _db.Specs.Where(s => s.Id == spec.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSpecApproved, true)
                .SetProperty(x => x.IsCodeDone, true)
                .SetProperty(x => x.IsAcPassed, (bool?)true)
                .SetProperty(x => x.CurrentStateName, "ac"));
        ClearTracker();

        await _svc.AdvanceSpec(spec.Id);

        await _svc.ApproveSpecHumanInLoop(spec.Id, new ApproveSpecHumanInLoopRequest(false, null));

        var result = await _svc.AdvanceSpec(spec.Id);

        Assert.Equal("coding", result.CurrentStateName);
    }

    // ── helper: advance epic through to a target state ───────────────────────

    private async Task<Epic> AdvanceEpicToSpecWriting()
    {
        var epic = await Create();

        await _svc.UpdateEpicField(epic.Id, "IsDocDrafted", "true");

        await Advance(epic.Id);

        await _svc.RaiseAgentSwarm(epic.Id, new RaiseAgentSwarmRequest("Align on scope", "spec_writing"));

        var entity = await _db.FindEpicOrThrow(epic.Id);
        var swarm = System.Text.Json.JsonSerializer.Deserialize<AgentSwarm>(entity.AgentSwarm!)!;
        foreach (var a in swarm.Agreements)
        {
            await _svc.SubmitAgreement(epic.Id, new SubmitAgreementRequest(a.AgentId, true, null));
        }

        await Advance(epic.Id);

        return await _svc.GetEpic(epic.Id);
    }

    private async Task<Epic> AdvanceEpicToHumanInLoop()
    {
        var epic = await AdvanceEpicToSpecWriting();

        await _svc.CreateSpec(epic.Id, new CreateSpecRequest("coding-agent-1", "/specs/spec-1.md", false, null));

        await _svc.RaiseAgentSwarm(epic.Id, new RaiseAgentSwarmRequest("Review spec list", "spec_writing"));
        var entity = await _db.FindEpicOrThrow(epic.Id);
        var swarm = System.Text.Json.JsonSerializer.Deserialize<AgentSwarm>(entity.AgentSwarm!)!;
        foreach (var a in swarm.Agreements)
        {
            await _svc.SubmitAgreement(epic.Id, new SubmitAgreementRequest(a.AgentId, true, null));
        }

        await Advance(epic.Id);

        return await _svc.GetEpic(epic.Id);
    }

    private async Task<Epic> AdvanceEpicToImplementation()
    {
        var epic = await AdvanceEpicToHumanInLoop();

        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(true, null));
        await Advance(epic.Id);

        return await _svc.GetEpic(epic.Id);
    }

    private void ClearTracker() => _db.ChangeTracker.Clear();

    private async Task SeedEpicEntity(string epicId)
    {
        if (await _db.Epics.AnyAsync(e => e.Id == epicId))
        {
            return;
        }

        _db.Epics.Add(new EpicEntity
        {
            Id = epicId,
            Name = "test",
            EpicAgent = "epic-agent-1",
            Slug = epicId,
            CodingAgents = "[]",
            CurrentStateName = "implementation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
