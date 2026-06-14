using EpicTracker.Contracts;
using EpicTracker.Lifecycles.EpicStates;
using EpicTracker.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EpicTracker.Tests;

public class SpecWritingPhase5Tests
{
    private static EpicContext BuildContext(Epic epic, FakeFileSystem fs) =>
        new()
        {
            Epic = epic,
            Logger = NullLogger.Instance,
            FileSystem = fs,
            Options = new() { EpicsBasePath = "/epics", GovernanceTemplatePath = "/gov.md" },
            Broker = null!
        };

    private static Epic BaseEpic(int phase = 5) => new()
    {
        Id = "epic-1",
        EpicAgentName = "pm",
        Slug = "my-epic",
        BasePath = "/",
        CurrentStateName = SpecWritingState.StateName,
        SpecWritingPhase = phase,
        CodingAgentNames = ["alice", "bob"]
    };

    private static Spec Spec(string id, string path) => new()
    {
        Id = id,
        EpicId = "epic-1",
        AssignedAgentName = "alice",
        SpecDocPath = path,
        CurrentStateName = "coding"
    };

    private static string FullySignedDoc(params string[] agents)
    {
        var lines = agents.Select(a => $"- [x] {a}");
        return $"## Reviewer\n{string.Join("\n", lines)}\n";
    }

    [Fact]
    public async Task Phase5_AllSigned_AdvancesToHumanInLoop()
    {
        var epic = BaseEpic();
        epic.Specs.Add(Spec("spec-1", "/specs/spec-1.md"));

        var fs = new FakeFileSystem();
        fs.Add("/specs/spec-1.md", FullySignedDoc("alice", "bob"));

        var state = new SpecWritingState();
        var next = await state.MoveNext(BuildContext(epic, fs));

        Assert.Equal("human_in_loop", next.Name);
    }

    [Fact]
    public async Task Phase5_UnsignedAgent_Blocks()
    {
        var epic = BaseEpic();
        epic.Specs.Add(Spec("spec-1", "/specs/spec-1.md"));

        var fs = new FakeFileSystem();
        fs.Add("/specs/spec-1.md", "## Reviewer\n- [x] alice\n- [ ] bob\n");

        var state = new SpecWritingState();
        var next = await state.MoveNext(BuildContext(epic, fs));

        Assert.Equal(SpecWritingState.StateName, next.Name);
        Assert.Contains("bob", epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task Phase5_MissingReviewerSection_Blocks()
    {
        var epic = BaseEpic();
        epic.Specs.Add(Spec("spec-1", "/specs/spec-1.md"));

        var fs = new FakeFileSystem();
        fs.Add("/specs/spec-1.md", "## Goal\nDo something.\n");

        var state = new SpecWritingState();
        var next = await state.MoveNext(BuildContext(epic, fs));

        Assert.Equal(SpecWritingState.StateName, next.Name);
        Assert.Contains("spec-1", epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task Phase5_MissingFile_Blocks()
    {
        var epic = BaseEpic();
        epic.Specs.Add(Spec("spec-1", "/specs/missing.md"));

        var state = new SpecWritingState();
        var next = await state.MoveNext(BuildContext(epic, new FakeFileSystem()));

        Assert.Equal(SpecWritingState.StateName, next.Name);
        Assert.Contains("spec-1", epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task Phase1_EmitsKickoffInstruction()
    {
        var epic = BaseEpic(phase: 1);

        var state = new SpecWritingState();
        var next = await state.MoveNext(BuildContext(epic, new FakeFileSystem()));

        Assert.Equal(SpecWritingState.StateName, next.Name);
        Assert.Contains("Phase 1", epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task Phase2_EmitsSpecCollectionInstruction()
    {
        var epic = BaseEpic(phase: 2);

        var state = new SpecWritingState();
        var next = await state.MoveNext(BuildContext(epic, new FakeFileSystem()));

        Assert.Equal(SpecWritingState.StateName, next.Name);
        Assert.Contains("Goldilocks", epic.EpicAgentInstruction);
    }

    [Fact]
    public async Task Rejection_ResetsPhaseAndAbandonSpecs()
    {
        var epic = BaseEpic(phase: 5);
        epic.Specs.Add(Spec("spec-1", "/specs/spec-1.md"));
        epic.HumanInLoop = new HumanInLoop
        {
            Questions = "Review specs",
            ApproveToStateName = "implementation",
            RejectToStateName = SpecWritingState.StateName,
            IsApproved = false,
            HumanInput = "Not happy with specs"
        };

        var state = new SpecWritingState();
        var next = await state.MoveNext(BuildContext(epic, new FakeFileSystem()));

        Assert.Equal(SpecWritingState.StateName, next.Name);
        Assert.Equal(1, epic.SpecWritingPhase);
        Assert.All(epic.Specs, s => Assert.True(s.IsAbandoned));
        Assert.Null(epic.HumanInLoop);
    }
}

internal class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string path, string content) => _files[path] = content;

    public bool FileExists(string? path) => path is not null && _files.ContainsKey(path);
    public string ReadAllText(string path) => _files[path];
    public void CreateDirectory(string path) { }
    public void CopyFile(string sourcePath, string destPath, bool overwrite = false) { }
}
