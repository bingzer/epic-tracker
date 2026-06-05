using System.Text.Json;
using System.Text.RegularExpressions;
using EpicTracker.Data;
using EpicTracker.Lifecycles.EpicStates;
using EpicTracker.Lifecycles.SpecStates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EpicTracker.Services;

/// <summary>
/// Single public entry point for all epic lifecycle operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Epic</b> is both the domain object and the state container — it holds all fields plus
/// CurrentStateName, EpicAgentInstruction, HumanInLoop, and AgentSwarm.
/// </para>
/// <para>
/// <b>EpicAudit</b> is append-only history — one row per Advance call.
/// HumanInLoop and AgentSwarm live on EpicEntity as mutable JSON columns so they can be
/// updated between Advance calls (e.g. submitting agreements, responding to human-in-loop).
/// </para>
/// <para>
/// <b>EpicState instances are stateless.</b> All mutable data lives on Epic.
/// A state returns <c>this</c> to signal "blocked, waiting for external input" — the Epic Agent
/// reads EpicAgentInstruction, acts, then calls Advance again.
/// </para>
/// <para>
/// Typical flow:
/// <list type="number">
///   <item>UI calls <see cref="CreateEpic"/> → entity inserted at state "drafting", first audit row seeded.</item>
///   <item>Epic Agent calls <see cref="GetEpic"/> on startup to read current state and instruction.</item>
///   <item>Epic Agent calls <see cref="Advance"/> in a loop; reads EpicAgentInstruction to know what to do next.</item>
///   <item>Agent acts (calls <see cref="SubmitAgreement"/>, UI calls <see cref="ApproveHumanInLoop"/>, etc.), then Advance again.</item>
///   <item>State machine transitions forward until a terminal state is reached.</item>
/// </list>
/// </para>
/// </remarks>
public class EpicService(EpicTrackerDbContext db, TmuxService tmux, ILogger<EpicService> logger, IFileSystem fileSystem, IOptions<EpicTrackerOptions> options, EpicScaffolding scaffolding)
{
    /// <summary>
    /// Creates a new epic and seeds it at the "drafting" state.
    /// </summary>
    public async Task<Epic> CreateEpic(CreateEpicRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var baseSlug = Slugify(request.Name, Guid.NewGuid().ToString());
        var slug = baseSlug;
        var counter = 2;

        while (await db.Epics.AnyAsync(e => e.Id == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter++}";
        }

        var entity = new EpicEntity
        {
            Id = slug,
            Name = request.Name,
            EpicAgentName = request.EpicAgentName,
            Brief = request.Brief,
            Slug = slug,
            NeedsMockup = request.NeedsMockup,
            ReviewerAgentName = request.ReviewerAgentName,
            CodingAgentNames = JsonSerializer.Serialize(request.CodingAgentNames ?? []),
            CurrentStateName = DraftingState.StateName,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Epics.Add(entity);

        var created = ToEpic(entity);

        db.EpicAudits.Add(EpicMapper.ToAudit(entity.Id, request.EpicAgentName, string.Empty, created));

        await db.SaveChangesAsync(cancellationToken);

        scaffolding.Scaffold(created, options.Value.GovernanceTemplatePath);

        return created;
    }

    private Epic ToEpic(EpicEntity entity) => EpicMapper.ToEpic(entity, options.Value.EpicsBasePath);

    private static string Slugify(string? name, string fallbackGuid)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"epic-{fallbackGuid[..8]}";
        }

        var slug = name.ToLowerInvariant().Replace(' ', '-');
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", string.Empty);
        slug = Regex.Replace(slug, @"-{2,}", "-").Trim('-');

        if (string.IsNullOrEmpty(slug))
        {
            return $"epic-{fallbackGuid[..8]}";
        }

        return slug;
    }

    /// <summary>
    /// Returns all epics.
    /// </summary>
    public async Task<List<Epic>> ListEpics(CancellationToken cancellationToken = default)
    {
        var entities = await db.Epics
            .Include(e => e.Specs.Where(s => !s.IsAbandoned))
            .ToListAsync(cancellationToken);

        return entities.Select(ToEpic).ToList();
    }

    /// <summary>
    /// Returns the current epic with its lifecycle state. Called by the Epic Agent on startup or resume.
    /// </summary>
    public async Task<Epic> GetEpic(string epicId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        return ToEpic(entity);
    }

    /// <summary>
    /// Returns the audit log for an epic in chronological order.
    /// </summary>
    public async Task<List<EpicAudit>> GetEpicHistory(string epicId, CancellationToken cancellationToken = default)
    {
        await db.FindEpicOrThrow(epicId, cancellationToken);

        var rows = await db.EpicAudits
            .Where(a => a.EpicId == epicId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(cancellationToken);

        return rows.Select(EpicMapper.ToEpicAudit).ToList();
    }

    /// <summary>
    /// Updates mutable epic fields. Intentionally broad — the MCP layer is responsible for restricting what the agent can change.
    /// </summary>
    /// <remarks>
    /// Does not touch Id, EpicAgent, CreatedAt, or CurrentStateName — those are immutable after creation.
    /// </remarks>
    public async Task<Epic> UpdateEpic(Epic epic, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epic.Id, cancellationToken);

        entity.Name = epic.Name;
        entity.Brief = epic.Brief;
        entity.Slug = epic.Slug;
        entity.NeedsMockup = epic.NeedsMockup;
        entity.IsDocDrafted = epic.IsDocDrafted;
        entity.IsMockupDone = epic.IsMockupDone;
        entity.CodingAgentNames = JsonSerializer.Serialize(epic.CodingAgentNames);
        entity.ReviewerAgentName = epic.ReviewerAgentName;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return ToEpic(entity);
    }

    public async Task<Epic> ForceEpicState(string epicId, string stateName, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);
        entity.CurrentStateName = stateName;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToEpic(entity);
    }

    /// <summary>
    /// Sets a single named field on an epic. The field name must be in the whitelist.
    /// </summary>
    public async Task<Epic> UpdateEpicField(string epicId, string fieldName, string value, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        switch (fieldName)
        {
            case "Name":
                entity.Name = value;
                entity.Slug = Slugify(value, entity.Id);
                break;

            case "Brief":
                entity.Brief = value;
                break;

            case "EpicAgentName":
                entity.EpicAgentName = value;
                break;

            case "CodingAgentNames":
                var agents = value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                entity.CodingAgentNames = JsonSerializer.Serialize(agents);
                break;

            case "NeedsMockup":
                entity.NeedsMockup = bool.Parse(value);
                break;

            case "IsDocDrafted":
                entity.IsDocDrafted = bool.Parse(value);
                break;

            case "IsMockupDone":
                entity.IsMockupDone = bool.Parse(value);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown field '{fieldName}'. Valid fields: Name, Brief, EpicAgentName, CodingAgentNames, NeedsMockup, IsDocDrafted, IsMockupDone.");
        }

        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return ToEpic(entity);
    }

    /// <summary>
    /// Sets a single named field on a spec. The field name must be in the whitelist.
    /// </summary>
    public async Task<Spec> UpdateSpecField(string specId, string fieldName, string value, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);

        switch (fieldName)
        {
            case "AssignedAgentName":
                entity.AssignedAgentName = value;
                break;

            case "ReviewerAgentName":
                entity.ReviewerAgentName = value;
                break;

            case "SpecDocPath":
                if (!Path.IsPathRooted(value))
                {
                    throw new InvalidOperationException($"SpecDocPath must be an absolute path. Got: '{value}'");
                }
                entity.SpecDocPath = value;
                break;

            case "CodeReviewRequired":
                entity.CodeReviewRequired = bool.Parse(value);
                break;

            case "IsSpecDrafted":
                entity.IsSpecDrafted = bool.Parse(value);
                break;

            case "IsCodeDone":
                entity.IsCodeDone = bool.Parse(value);
                break;

            case "IsAcPassed":
                entity.IsAcPassed = bool.Parse(value);
                break;

            case "IsCodeReviewApproved":
                entity.IsCodeReviewApproved = bool.Parse(value);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown field '{fieldName}'. Valid fields: AssignedAgentName, ReviewerAgentName, SpecDocPath, CodeReviewRequired, IsSpecDrafted, IsCodeDone, IsAcPassed, IsCodeReviewApproved.");
        }

        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return await AdvanceSpec(specId, cancellationToken);
    }

    /// <summary>
    /// Raises an agent swarm. Called by the Epic Agent via MCP to initiate a multi-agent consensus round.
    /// </summary>
    /// <remarks>
    /// The swarm transitions to <paramref name="toStateName"/> when consensus is reached.
    /// If max iterations are exceeded without consensus, it escalates to HumanInLoop automatically.
    /// </remarks>
    public async Task RaiseAgentSwarm(string epicId, RaiseAgentSwarmRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        var epic = ToEpic(entity);

        epic.AgentSwarm = new AgentSwarm
        {
            Objective = request.Objective,
            ToStateName = request.ToStateName,
            Agreements = epic.CodingAgentNames
                .Select(id => new AgentAgreement { AgentId = id })
                .Append(new AgentAgreement { AgentId = epic.EpicAgentName })
                .ToList()
        };

        EpicMapper.SyncToEntity(epic, entity);

        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Raises a human-in-loop request. Called by the Epic Agent via MCP when it needs human input before proceeding.
    /// </summary>
    public async Task RaiseHumanInLoop(string epicId, RaiseHumanInLoopRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        var epic = ToEpic(entity);

        epic.HumanInLoop = new HumanInLoop
        {
            Questions = request.Questions,
            ApproveToStateName = request.ApproveToStateName,
            RejectToStateName = request.RejectToStateName
        };

        EpicMapper.SyncToEntity(epic, entity);

        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Records the human response to a human-in-loop request. UI only — not exposed via MCP.
    /// </summary>
    public async Task ApproveHumanInLoop(string epicId, ApproveEpicHumanInLoopRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        var epic = ToEpic(entity);

        if (epic.HumanInLoop is null)
        {
            throw new InvalidOperationException($"No active HumanInLoop for epic {epicId}.");
        }

        epic.HumanInLoop.IsApproved = request.IsApproved;
        epic.HumanInLoop.HumanInput = request.HumanInput;

        EpicMapper.SyncToEntity(epic, entity);

        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await tmux.SendKeys(entity.EpicAgentName, $"Human {(request.IsApproved ? "approved" : "rejected")} epic {epicId}. Call advance.", cancellationToken);
    }

    /// <summary>
    /// Submits a coding agent's agreement or disagreement on the current swarm objective.
    /// Called by the Epic Agent via MCP after collecting responses from coding agents via tmux-broker.
    /// </summary>
    public async Task SubmitAgreement(string epicId, SubmitAgreementRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        var epic = ToEpic(entity);

        if (epic.AgentSwarm is null)
        {
            throw new InvalidOperationException($"No active AgentSwarm for epic {epicId}.");
        }

        var agreement = epic.AgentSwarm.Agreements.FirstOrDefault(a => a.AgentId == request.AgentId);

        if (agreement is null)
        {
            if (!epic.CodingAgentNames.Contains(request.AgentId) && epic.EpicAgentName != request.AgentId)
            {
                throw new InvalidOperationException($"Agent {request.AgentId} is not part of the swarm or codingAgents for epic {epicId}.");
            }

            agreement = new AgentAgreement { AgentId = request.AgentId };
            epic.AgentSwarm.Agreements.Add(agreement);
        }

        agreement.HasAgreed = request.HasAgreed;
        agreement.Note = request.Note;

        EpicMapper.SyncToEntity(epic, entity);

        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WakeAgent(string epicId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        var message = entity.CurrentStateName == DraftingState.StateName
            ? $"Let's work on {entity.Id}. Call get_epic then advance."
            : $"Hey, are you still working on epic {entity.Id}? If not, please continue — call get_epic(\"{entity.Id}\") first to get the latest state, then carry on.";

        await tmux.SendKeys(entity.EpicAgentName, message, cancellationToken);
    }

    public async Task DeleteEpic(string epicId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);
        db.Epics.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Advances the epic lifecycle one step. Called by the Epic Agent via MCP.
    /// </summary>
    /// <remarks>
    /// Reconstructs Epic from DB, calls MoveNext on the current state, then persists the result.
    /// The returned Epic's EpicAgentInstruction tells the agent what to do next.
    /// Throws if <paramref name="epicAgentId"/> does not match the epic's assigned agent.
    /// </remarks>
    public async Task<Epic> Advance(string epicId, AdvanceEpicRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        if (entity.EpicAgentName != request.EpicAgentId)
        {
            throw new InvalidOperationException($"{request.EpicAgentId} is not the Epic Agent for epic {epicId}.");
        }

        var epic = ToEpic(entity);

        var fromState = epic.CurrentStateName;

        var epicContext = new EpicContext { Epic = epic, Logger = logger, FileSystem = fileSystem };

        var currentState = EpicState.CreateEpicState(epic.CurrentStateName);
        string previousName;

        do
        {
            previousName = currentState.Name;
            currentState = await currentState.MoveNext(epicContext, cancellationToken);
            epic.CurrentStateName = currentState.Name;
        } while (currentState.Name != previousName);

        entity.CurrentStateName = epic.CurrentStateName;
        entity.UpdatedAt = DateTime.UtcNow;

        EpicMapper.SyncToEntity(epic, entity);

        foreach (var spec in epic.Specs)
        {
            var specEntity = entity.Specs.First(s => s.Id == spec.Id);
            EpicMapper.SyncSpecToEntity(spec, specEntity);
            specEntity.UpdatedAt = DateTime.UtcNow;
        }

        db.EpicAudits.Add(EpicMapper.ToAudit(epicId, request.EpicAgentId, fromState, epic));

        await db.SaveChangesAsync(cancellationToken);

        return epic;
    }

    /// <summary>
    /// Creates a new spec under an epic and seeds it at the "spec_drafting" state.
    /// Called by the Epic Agent via MCP after a coding agent submits a spec path.
    /// </summary>
    public async Task<Spec> CreateSpec(string epicId, CreateSpecRequest request, CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathRooted(request.SpecDocPath))
        {
            throw new InvalidOperationException($"SpecDocPath must be an absolute path. Got: '{request.SpecDocPath}'");
        }

        await db.FindEpicOrThrow(epicId, cancellationToken);

        var now = DateTime.UtcNow;

        var baseSlug = $"{epicId}-{Slugify(request.SpecName, Guid.NewGuid().ToString())}";
        var slug = baseSlug;
        var counter = 2;

        while (await db.Specs.AnyAsync(s => s.Id == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter++}";
        }

        var entity = new SpecEntity
        {
            Id = slug,
            EpicId = epicId,
            AssignedAgentName = request.AssignedAgentName,
            ReviewerAgentName = request.ReviewerAgentName,
            CodeReviewRequired = request.CodeReviewRequired,
            SpecDocPath = request.SpecDocPath,
            CurrentStateName = DraftingSpecState.StateName,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Specs.Add(entity);

        await db.SaveChangesAsync(cancellationToken);

        return EpicMapper.ToSpec(entity);
    }

    /// <summary>
    /// Returns the current spec.
    /// </summary>
    public async Task<Spec> GetSpec(string specId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);

        return EpicMapper.ToSpec(entity);
    }

    /// <summary>
    /// Updates mutable spec flags from the REST endpoint. Used by the UI dashboard.
    /// </summary>
    public async Task<Spec> UpdateSpec(Spec spec, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(spec.Id, cancellationToken);

        entity.SpecDocPath = spec.SpecDocPath;
        entity.IsSpecDrafted = spec.IsSpecDrafted;
        entity.IsCodeDone = spec.IsCodeDone;
        entity.IsCodeReviewApproved = spec.IsCodeReviewApproved;
        entity.IsAcPassed = spec.IsAcPassed;
        entity.CodeReviewRequired = spec.CodeReviewRequired;
        entity.ReviewerAgentName = spec.ReviewerAgentName;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return await AdvanceSpec(spec.Id, cancellationToken);
    }

    /// <summary>
    /// Records the human response to a spec human-in-loop request. UI only.
    /// </summary>
    public async Task ApproveSpecHumanInLoop(string specId, ApproveSpecHumanInLoopRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);

        var spec = EpicMapper.ToSpec(entity);

        if (spec.HumanInLoop is null)
        {
            throw new InvalidOperationException($"No active HumanInLoop for spec {specId}.");
        }

        spec.HumanInLoop.IsApproved = request.IsApproved;
        spec.HumanInLoop.HumanInput = request.HumanInput;

        EpicMapper.SyncSpecToEntity(spec, entity);

        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var epicEntity = await db.FindEpicOrThrow(entity.EpicId, cancellationToken);
        await tmux.SendKeys(epicEntity.EpicAgentName, $"Human {(request.IsApproved ? "approved" : "rejected")} spec {specId}. Call advance_spec then advance.", cancellationToken);
    }

    public async Task<Spec> MarkSpecReadyToCode(string specId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);

        entity.IsReadyToCode = true;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return await AdvanceSpec(specId, cancellationToken);
    }

    public async Task<Spec> ForceSpecState(string specId, string stateName, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);
        entity.CurrentStateName = stateName;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return EpicMapper.ToSpec(entity);
    }

    /// <summary>
    /// Advances the spec lifecycle one step. Called by the Epic Agent via MCP.
    /// </summary>
    public async Task<Spec> AdvanceSpec(string specId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);

        var spec = EpicMapper.ToSpec(entity);

        var currentState = SpecState.CreateSpecState(spec.CurrentStateName);

        var specContext = new SpecContext { Spec = spec, Logger = logger, FileSystem = fileSystem };

        var nextState = await currentState.MoveNext(specContext, cancellationToken);

        spec.CurrentStateName = nextState.Name;

        EpicMapper.SyncSpecToEntity(spec, entity);

        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return spec;
    }


}

