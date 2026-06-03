using EpicTracker.Contracts;
using EpicTracker.Data;
using EpicTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EpicTracker.Tests;

public class HappyPathTests : IDisposable
{
    private readonly EpicTrackerDbContext _db;
    private readonly EpicService _svc;

    public HappyPathTests()
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

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task AllAgentsAgree(string epicId)
    {
        var entity = await _db.FindEpicOrThrow(epicId);
        var swarm = System.Text.Json.JsonSerializer.Deserialize<AgentSwarm>(entity.AgentSwarm!)!;

        foreach (var agreement in swarm.Agreements)
        {
            await _svc.SubmitAgreement(epicId, new SubmitAgreementRequest(agreement.AgentId, true, null));
        }
    }

    private async Task<Epic> Advance(string epicId) =>
        await _svc.Advance(epicId, new AdvanceEpicRequest("epic-agent-1"));

    // ── end-to-end nominal path ───────────────────────────────────────────────

    [Fact]
    public async Task FullEpicLifecycle_NominalPath()
    {
        // 1. Create epic (no mockup, no code review)
        var epic = await _svc.CreateEpic(new CreateEpicRequest(
            EpicAgent: "epic-agent-1",
            Brief: "Integration test",
            Name: "Full Lifecycle Epic",
            CodingAgents: ["ca-1"],
            NeedsMockup: false,
            ReviewerAgentId: null));

        Assert.Equal("drafting", epic.CurrentStateName);

        // 2. Set IsDocDrafted = true
        await _svc.UpdateEpicField(epic.Id, "IsDocDrafted", "true");

        // 3. Advance → waterproofing
        var afterDrafting = await Advance(epic.Id);
        Assert.Equal("waterproofing", afterDrafting.CurrentStateName);

        // 4. RaiseAgentSwarm, all agree, Advance → spec_writing
        await _svc.RaiseAgentSwarm(epic.Id, new RaiseAgentSwarmRequest("Align on scope", "spec_writing"));
        await AllAgentsAgree(epic.Id);
        var afterWaterproofing = await Advance(epic.Id);
        Assert.Equal("spec_writing", afterWaterproofing.CurrentStateName);

        // 5. CreateSpec (no code review)
        var spec = await _svc.CreateSpec(epic.Id, new CreateSpecRequest("ca-1", "/specs/spec-1.md", false, null));

        // 6. RaiseAgentSwarm for spec review, all agree, Advance → human_in_loop
        await _svc.RaiseAgentSwarm(epic.Id, new RaiseAgentSwarmRequest("Review spec list", "spec_writing"));
        await AllAgentsAgree(epic.Id);
        var afterSpecReview = await Advance(epic.Id);
        Assert.Equal("human_in_loop", afterSpecReview.CurrentStateName);

        // 7. ApproveHumanInLoop(true), Advance → implementation
        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(true, null));
        var afterHilApprove = await Advance(epic.Id);
        Assert.Equal("implementation", afterHilApprove.CurrentStateName);

        // 8. Advance (implementation sets IsSpecApproved, sees spec in drafting, blocks)
        var blockedImpl = await Advance(epic.Id);
        Assert.Equal("implementation", blockedImpl.CurrentStateName);

        // 9. AdvanceSpec (spec is now approved → transitions to coding)
        var specAfterApproval = await _svc.AdvanceSpec(spec.Id);
        Assert.Equal("coding", specAfterApproval.CurrentStateName);

        // 10. UpdateSpec (set IsCodeDone = true), AdvanceSpec → ac
        await _svc.UpdateSpec(new Spec
        {
            Id = spec.Id,
            EpicId = spec.EpicId,
            AssignedAgentId = spec.AssignedAgentId,
            SpecDocPath = spec.SpecDocPath,
            IsCodeDone = true
        });

        var specInAc = await _svc.AdvanceSpec(spec.Id);
        Assert.Equal("ac", specInAc.CurrentStateName);

        // 11. UpdateSpec (set IsAcPassed = true), AdvanceSpec → spec_human_in_loop
        await _svc.UpdateSpec(new Spec
        {
            Id = spec.Id,
            EpicId = spec.EpicId,
            AssignedAgentId = spec.AssignedAgentId,
            SpecDocPath = spec.SpecDocPath,
            IsCodeDone = true,
            IsAcPassed = true
        });

        var specInHil = await _svc.AdvanceSpec(spec.Id);
        Assert.Equal("spec_human_in_loop", specInHil.CurrentStateName);

        // 12. ApproveSpecHumanInLoop(true), AdvanceSpec → done
        await _svc.ApproveSpecHumanInLoop(spec.Id, new ApproveSpecHumanInLoopRequest(true, null));
        var specDone = await _svc.AdvanceSpec(spec.Id);
        Assert.Equal("done", specDone.CurrentStateName);

        // 13. Advance (all specs done → human_in_loop)
        var epicHil = await Advance(epic.Id);
        Assert.Equal("human_in_loop", epicHil.CurrentStateName);

        // 14. ApproveHumanInLoop(true), Advance → closed
        await _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(true, null));
        var closed = await Advance(epic.Id);

        Assert.Equal("closed", closed.CurrentStateName);

        // Audit log has entries
        var auditCount = await _db.EpicAudits.CountAsync(a => a.EpicId == epic.Id);
        Assert.True(auditCount > 0);
    }
}
