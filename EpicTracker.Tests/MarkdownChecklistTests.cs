using EpicTracker.Lifecycles.SpecStates;

namespace EpicTracker.Tests;

public class MarkdownChecklistTests
{
    [Fact]
    public void Returns_empty_when_section_not_found()
    {
        var md = """
            ## Other Section
            - [ ] Item
            """;

        var result = MarkdownChecklist.Parse(md, "## Acceptance Criteria");

        Assert.Empty(result);
    }

    [Fact]
    public void Returns_empty_when_section_has_no_checkboxes()
    {
        var md = """
            ## Acceptance Criteria
            Some prose with no checkboxes.

            ## Next Section
            """;

        var result = MarkdownChecklist.Parse(md, "## Acceptance Criteria");

        Assert.Empty(result);
    }

    [Fact]
    public void Parses_unchecked_item()
    {
        var md = """
            ## Acceptance Criteria
            - [ ] Do the thing
            """;

        var result = MarkdownChecklist.Parse(md, "## Acceptance Criteria");

        Assert.Single(result);
        Assert.Equal("Do the thing", result[0].Name);
        Assert.False(result[0].IsChecked);
    }

    [Fact]
    public void Parses_checked_item_lowercase()
    {
        var md = """
            ## Acceptance Criteria
            - [x] Do the thing
            """;

        var result = MarkdownChecklist.Parse(md, "## Acceptance Criteria");

        Assert.Single(result);
        Assert.Equal("Do the thing", result[0].Name);
        Assert.True(result[0].IsChecked);
    }

    [Fact]
    public void Parses_checked_item_uppercase()
    {
        var md = """
            ## Acceptance Criteria
            - [X] Do the thing
            """;

        var result = MarkdownChecklist.Parse(md, "## Acceptance Criteria");

        Assert.Single(result);
        Assert.True(result[0].IsChecked);
    }

    [Fact]
    public void Parses_mixed_checked_and_unchecked()
    {
        var md = """
            ## Acceptance Criteria
            - [x] Item one
            - [ ] Item two
            - [x] Item three
            """;

        var result = MarkdownChecklist.Parse(md, "## Acceptance Criteria");

        Assert.Equal(3, result.Count);
        Assert.True(result[0].IsChecked);
        Assert.False(result[1].IsChecked);
        Assert.True(result[2].IsChecked);
    }

    [Fact]
    public void Stops_at_next_section_header()
    {
        var md = """
            ## Acceptance Criteria
            - [ ] Item one
            - [x] Item two

            ## Development Plan
            - [ ] Should not be included
            """;

        var result = MarkdownChecklist.Parse(md, "## Acceptance Criteria");

        Assert.Equal(2, result.Count);
        Assert.Equal("Item one", result[0].Name);
        Assert.Equal("Item two", result[1].Name);
    }

    [Fact]
    public void Parses_correct_section_when_multiple_exist()
    {
        var md = """
            ## Acceptance Criteria
            - [x] AC item

            ## Development Plan
            - [ ] Plan item
            """;

        var result = MarkdownChecklist.Parse(md, "## Development Plan");

        Assert.Single(result);
        Assert.Equal("Plan item", result[0].Name);
        Assert.False(result[0].IsChecked);
    }

    [Fact]
    public void Parses_full_spec_doc()
    {
        var md = """
            # Spec: my-spec

            ## Assigned Agent
            knowledgetree

            ## Goal
            Do a thing.

            ## Acceptance Criteria
            - [x] Feature works end to end
            - [ ] Edge case handled

            ## Development Plan
            - [x] Write the code
            - [x] Write the tests

            ## Deliverables
            - [x] /agents/pm/epics/my-epic/output/report.md
            """;

        var ac = MarkdownChecklist.Parse(md, "## Acceptance Criteria");
        var plan = MarkdownChecklist.Parse(md, "## Development Plan");
        var deliverables = MarkdownChecklist.Parse(md, "## Deliverables");

        Assert.Equal(2, ac.Count);
        Assert.True(ac[0].IsChecked);
        Assert.False(ac[1].IsChecked);

        Assert.Equal(2, plan.Count);
        Assert.All(plan, i => Assert.True(i.IsChecked));

        Assert.Single(deliverables);
        Assert.Equal("/agents/pm/epics/my-epic/output/report.md", deliverables[0].Name);
        Assert.True(deliverables[0].IsChecked);
    }
}
