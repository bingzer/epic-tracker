using EpicTracker.Lifecycles.EpicStates;
using EpicTracker.Lifecycles.SpecStates;

namespace EpicTracker.Tests;

public class StateFactoryTests
{
    // ── EpicState ─────────────────────────────────────────────────────────────

    [Fact]
    public void EpicState_Create_Throws_ForUnknownStateName()
    {
        Assert.Throws<InvalidOperationException>(
            () => EpicState.Create("not_a_real_state"));
    }

    [Theory]
    [InlineData("drafting")]
    [InlineData("waterproofing")]
    [InlineData("mockup")]
    [InlineData("agent_swarm")]
    [InlineData("human_in_loop")]
    [InlineData("spec_writing")]
    [InlineData("implementation")]
    [InlineData("closed")]
    public void EpicState_Create_ResolvesAllKnownStates(string stateName)
    {
        var state = EpicState.Create(stateName);

        Assert.NotNull(state);
        Assert.Equal(stateName, state.Name);
    }

    // ── SpecState ─────────────────────────────────────────────────────────────

    [Fact]
    public void SpecState_Create_Throws_ForUnknownStateName()
    {
        Assert.Throws<InvalidOperationException>(
            () => SpecState.Create("not_a_real_spec_state"));
    }

    [Theory]
    [InlineData("spec_drafting")]
    [InlineData("coding")]
    [InlineData("code_review")]
    [InlineData("ac")]
    [InlineData("spec_human_in_loop")]
    [InlineData("done")]
    public void SpecState_Create_ResolvesAllKnownStates(string stateName)
    {
        var state = SpecState.Create(stateName);

        Assert.NotNull(state);
        Assert.Equal(stateName, state.Name);
    }
}
