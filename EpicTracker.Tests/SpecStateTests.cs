using EpicTracker.Contracts;
using EpicTracker.Lifecycles.SpecStates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EpicTracker.Tests;

public class SpecStateTests
{
    private static Spec BaseSpec(bool codeReviewRequired = false) => new()
    {
        Id = "spec-1",
        EpicId = "epic-1",
        AssignedAgentId = "coding-agent-1",
        ReviewerAgentId = codeReviewRequired ? "reviewer-1" : null,
        CodeReviewRequired = codeReviewRequired,
        SpecDocPath = "/specs/spec-1.md",
        CurrentStateName = "spec_drafting"
    };

    // ── DraftingSpecState ────────────────────────────────────────────────────

    [Fact]
    public async Task DraftingSpecState_BlocksWhenNotApproved()
    {
        var spec = BaseSpec();
        spec.IsSpecApproved = false;
        var state = new DraftingSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("spec_drafting", next.Name);
        Assert.Null(spec.EpicAgentInstruction);
    }

    [Fact]
    public async Task DraftingSpecState_AdvancesToCoding_WhenApproved()
    {
        var spec = BaseSpec();
        spec.IsSpecApproved = true;
        var state = new DraftingSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("coding", next.Name);
        Assert.NotNull(spec.EpicAgentInstruction);
        Assert.Contains("coding-agent-1", spec.EpicAgentInstruction);
    }

    // ── CodingSpecState ──────────────────────────────────────────────────────

    [Fact]
    public async Task CodingSpecState_BlocksWhenCodeNotDone()
    {
        var spec = BaseSpec();
        spec.IsCodeDone = false;
        var state = new CodingSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("coding", next.Name);
        Assert.NotNull(spec.EpicAgentInstruction);
    }

    [Fact]
    public async Task CodingSpecState_AdvancesToAc_WhenCodeDone_NoReview()
    {
        var spec = BaseSpec(codeReviewRequired: false);
        spec.IsCodeDone = true;
        var state = new CodingSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("ac", next.Name);
        Assert.NotNull(spec.EpicAgentInstruction);
    }

    [Fact]
    public async Task CodingSpecState_AdvancesToCodeReview_WhenCodeDone_ReviewRequired()
    {
        var spec = BaseSpec(codeReviewRequired: true);
        spec.IsCodeDone = true;
        var state = new CodingSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("code_review", next.Name);
        Assert.NotNull(spec.EpicAgentInstruction);
    }

    // ── CodeReviewSpecState ──────────────────────────────────────────────────

    [Fact]
    public async Task CodeReviewSpecState_BlocksWhenNoDecision()
    {
        var spec = BaseSpec(codeReviewRequired: true);
        spec.IsCodeReviewApproved = null;
        var state = new CodeReviewSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("code_review", next.Name);
        Assert.Contains("reviewer-1", spec.EpicAgentInstruction);
    }

    [Fact]
    public async Task CodeReviewSpecState_AdvancesToAc_WhenApproved()
    {
        var spec = BaseSpec(codeReviewRequired: true);
        spec.IsCodeReviewApproved = true;
        var state = new CodeReviewSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("ac", next.Name);
        Assert.NotNull(spec.EpicAgentInstruction);
    }

    [Fact]
    public async Task CodeReviewSpecState_ResetsToCoding_WhenRejected()
    {
        var spec = BaseSpec(codeReviewRequired: true);
        spec.IsCodeDone = true;
        spec.IsCodeReviewApproved = false;
        var state = new CodeReviewSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("coding", next.Name);
        Assert.False(spec.IsCodeDone);
        Assert.Null(spec.IsCodeReviewApproved);
    }

    // ── AcSpecState ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AcSpecState_BlocksWhenAcNotRun()
    {
        var spec = BaseSpec();
        spec.IsAcPassed = null;
        var state = new AcSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("ac", next.Name);
        Assert.Contains("coding-agent-1", spec.EpicAgentInstruction);
    }

    [Fact]
    public async Task AcSpecState_ResetsToCoding_WhenFailed()
    {
        var spec = BaseSpec();
        spec.IsCodeDone = true;
        spec.IsAcPassed = false;
        var state = new AcSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("coding", next.Name);
        Assert.False(spec.IsCodeDone);
        Assert.Null(spec.IsAcPassed);
    }

    [Fact]
    public async Task AcSpecState_RaisesHumanInLoop_WhenPassed()
    {
        var spec = BaseSpec();
        spec.IsAcPassed = true;
        var state = new AcSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("spec_human_in_loop", next.Name);
        Assert.NotNull(spec.HumanInLoop);
        Assert.Equal("done", spec.HumanInLoop!.ApproveToStateName);
        Assert.Equal("coding", spec.HumanInLoop!.RejectToStateName);
    }

    [Fact]
    public async Task AcSpecState_ResetsToCoding_WhenHumanRejects()
    {
        var spec = BaseSpec();
        spec.IsCodeDone = true;
        spec.IsAcPassed = true;
        spec.HumanInLoop = new HumanInLoop
        {
            IsApproved = false,
            ApproveToStateName = "done",
            RejectToStateName = "coding"
        };
        var state = new AcSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("coding", next.Name);
        Assert.False(spec.IsCodeDone);
        Assert.Null(spec.IsAcPassed);
        Assert.Null(spec.HumanInLoop);
    }

    [Fact]
    public async Task AcSpecState_AdvancesToDone_WhenHumanApproves()
    {
        var spec = BaseSpec();
        spec.IsAcPassed = true;
        spec.HumanInLoop = new HumanInLoop
        {
            IsApproved = true,
            ApproveToStateName = "done",
            RejectToStateName = "coding"
        };
        var state = new AcSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("done", next.Name);
        Assert.Null(spec.HumanInLoop);
        Assert.NotNull(spec.EpicAgentInstruction);
    }

    // ── HumanInLoopSpecState ─────────────────────────────────────────────────

    [Fact]
    public async Task HumanInLoopSpecState_Throws_WhenNoHumanInLoop()
    {
        var spec = BaseSpec();
        var state = new HumanInLoopSpecState();

        await Assert.ThrowsAsync<InvalidOperationException>(() => state.MoveNext(spec, NullLogger.Instance));
    }

    [Fact]
    public async Task HumanInLoopSpecState_BlocksWhenNotAnswered()
    {
        var spec = BaseSpec();
        spec.HumanInLoop = new HumanInLoop
        {
            Questions = "Approve?",
            ApproveToStateName = "done",
            RejectToStateName = "coding"
        };
        var state = new HumanInLoopSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("spec_human_in_loop", next.Name);
        Assert.Contains("Waiting for human response", spec.EpicAgentInstruction);
    }

    [Fact]
    public async Task HumanInLoopSpecState_RoutesToApproveState()
    {
        var spec = BaseSpec();
        spec.HumanInLoop = new HumanInLoop
        {
            ApproveToStateName = "done",
            RejectToStateName = "coding",
            IsApproved = true
        };
        var state = new HumanInLoopSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("done", next.Name);
        Assert.Null(spec.HumanInLoop);
    }

    [Fact]
    public async Task HumanInLoopSpecState_RoutesToRejectState()
    {
        var spec = BaseSpec();
        spec.HumanInLoop = new HumanInLoop
        {
            ApproveToStateName = "done",
            RejectToStateName = "coding",
            IsApproved = false
        };
        var state = new HumanInLoopSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("coding", next.Name);
        Assert.Null(spec.HumanInLoop);
    }

    // ── DoneSpecState ────────────────────────────────────────────────────────

    [Fact]
    public async Task DoneSpecState_IsTerminal()
    {
        var spec = BaseSpec();
        var state = new DoneSpecState();

        var next = await state.MoveNext(spec, NullLogger.Instance);

        Assert.Equal("done", next.Name);
    }
}
