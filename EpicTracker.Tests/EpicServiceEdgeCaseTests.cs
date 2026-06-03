using EpicTracker.Contracts;
using EpicTracker.Data;
using EpicTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace EpicTracker.Tests;

public class EpicServiceEdgeCaseTests : IDisposable
{
    private readonly EpicTrackerDbContext _db;
    private readonly EpicService _svc;

    public EpicServiceEdgeCaseTests()
    {
        var options = new DbContextOptionsBuilder<EpicTrackerDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new EpicTrackerDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _svc = new EpicService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Epic> CreateEpic() => await _svc.CreateEpic(new Epic
    {
        Name = "Edge Case Epic",
        EpicAgent = "epic-agent-1",
        EpicDocumentPath = "/epics/e.md",
        EpicGovernancePath = "/epics/g.md",
        CodingAgents = ["ca-1", "ca-2"]
    });

    private async Task<Spec> CreateSpec(string epicId) =>
        await _svc.CreateSpec(epicId, new CreateSpecRequest("ca-1", "/specs/s.md", false, null));

    // ── GetEpic not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetEpic_Throws_WhenNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.GetEpic("does-not-exist"));
    }

    // ── ApproveHumanInLoop no HumanInLoop ────────────────────────────────────

    [Fact]
    public async Task ApproveHumanInLoop_Throws_WhenNoHumanInLoop()
    {
        var epic = await CreateEpic();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.ApproveHumanInLoop(epic.Id, new ApproveEpicHumanInLoopRequest(true, null)));
    }

    // ── SubmitAgreement no swarm ──────────────────────────────────────────────

    [Fact]
    public async Task SubmitAgreement_Throws_WhenNoSwarm()
    {
        var epic = await CreateEpic();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.SubmitAgreement(epic.Id, new SubmitAgreementRequest("ca-1", true, null)));
    }

    // ── SubmitAgreement agent not in swarm ────────────────────────────────────

    [Fact]
    public async Task SubmitAgreement_Throws_WhenAgentNotInSwarm()
    {
        var epic = await CreateEpic();

        await _svc.RaiseAgentSwarm(epic.Id, new RaiseAgentSwarmRequest("Align", "spec_writing"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.SubmitAgreement(epic.Id, new SubmitAgreementRequest("unknown-agent", true, null)));
    }

    // ── ApproveSpecHumanInLoop no HumanInLoop ─────────────────────────────────

    [Fact]
    public async Task ApproveSpecHumanInLoop_Throws_WhenNoHumanInLoop()
    {
        var epic = await CreateEpic();
        var spec = await CreateSpec(epic.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.ApproveSpecHumanInLoop(spec.Id, new ApproveSpecHumanInLoopRequest(true, null)));
    }

    // ── AdvanceSpec not found ─────────────────────────────────────────────────

    [Fact]
    public async Task AdvanceSpec_Throws_WhenNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.AdvanceSpec("does-not-exist"));
    }

    // ── Audit rows written on every Advance ───────────────────────────────────

    [Fact]
    public async Task Advance_WritesAuditRow_OnEveryCall()
    {
        var epic = await CreateEpic();

        await _svc.Advance(epic.Id, new AdvanceEpicRequest("epic-agent-1"));
        await _svc.Advance(epic.Id, new AdvanceEpicRequest("epic-agent-1"));

        var auditCount = await _db.EpicAudits.CountAsync(a => a.EpicId == epic.Id);

        Assert.Equal(3, auditCount);
    }
}
