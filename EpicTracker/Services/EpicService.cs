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
public class EpicService(EpicTrackerDbContext db, TmuxService tmux, ILogger<EpicService> logger, IFileSystem fileSystem, IOptions<EpicTrackerOptions> options, EpicScaffolding scaffolding, BrokerService broker)
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

        while (await db.Epics.AnyAsync(e => e.Slug == slug, cancellationToken))
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
            IsACRequired = request.IsACRequired,
            IsCodeReviewRequired = request.IsCodeReviewRequired ?? (request.ReviewerAgentName != null),
            ReviewerAgentName = request.ReviewerAgentName,
            CodingAgentNames = JsonSerializer.Serialize(request.CodingAgentNames ?? []),
            CurrentStateName = DraftingState.StateName,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Epics.Add(entity);

        var created = ToEpic(entity);

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicCreated,
            epicState: entity.CurrentStateName,
            epicId: entity.Id,
            actor: request.EpicAgentName,
            message: new { name = request.Name }
        ));

        await broker.CreateChannel($"epic-{created.Id}", created.EpicAgentName, cancellationToken);

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

        if (slug.Length > 35)
        {
            slug = slug[..35].TrimEnd('-');
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
    public async Task<List<AuditLog>> GetEpicHistory(string epicId, CancellationToken cancellationToken = default)
    {
        await db.FindEpicOrThrow(epicId, cancellationToken);

        var rows = await db.AuditLogs
            .Where(a => a.EpicId == epicId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(cancellationToken);

        return rows.Select(EpicMapper.ToAuditLog).ToList();
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
        entity.NeedsMockup = epic.NeedsMockup;
        entity.IsDocDrafted = epic.IsDocDrafted;
        entity.IsMockupDone = epic.IsMockupDone;
        entity.CodingAgentNames = JsonSerializer.Serialize(epic.CodingAgentNames);
        entity.ReviewerAgentName = epic.ReviewerAgentName;
        entity.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicUpdated,
            epicState: entity.CurrentStateName,
            epicId: epic.Id,
            actor: "human",
            message: new { name = epic.Name }
        ));

        await db.SaveChangesAsync(cancellationToken);

        return ToEpic(entity);
    }

    public async Task<Epic> ForceEpicState(string epicId, string stateName, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);
        var fromState = entity.CurrentStateName;
        entity.CurrentStateName = stateName;
        entity.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicForceState,
            epicState: stateName,
            epicId: epicId,
            actor: "human",
            message: new { from = fromState, to = stateName }
        ));
        await db.SaveChangesAsync(cancellationToken);
        return ToEpic(entity);
    }

    /// <summary>
    /// Sets a single named field on an epic. The field name must be in the whitelist.
    /// </summary>
    public async Task<Epic> UpdateEpicField(string epicId, string fieldName, string value, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        var epic = ToEpic(entity);
        var epicContext = new EpicContext { Epic = epic, Logger = logger, FileSystem = fileSystem, Options = options.Value, Broker = broker };
        var currentState = EpicState.CreateEpicState(entity.CurrentStateName);

        if (!currentState.UpdateEpicField(epicContext, fieldName, value))
        {
            throw new InvalidOperationException($"Cannot set field '{fieldName}' on epic at state '{currentState.Name}'.");
        }

        EpicMapper.SyncToEntity(epic, entity);

        if (fieldName == nameof(Epic.Name))
        {
            var baseSlug = Slugify(epic.Name, entity.Id);
            var slug = baseSlug;
            var counter = 2;
            while (await db.Epics.AnyAsync(e => e.Slug == slug && e.Id != entity.Id, cancellationToken))
            {
                slug = $"{baseSlug}-{counter++}";
            }
            entity.Slug = slug;
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
        var epicEntity = await db.FindEpicOrThrow(entity.EpicId, cancellationToken);

        // use current state of that spec to validate and set the field value, then save and advance
        var specContext = new SpecContext
        {
            Spec = EpicMapper.ToSpec(entity),
            Epic = ToEpic(epicEntity),
            Logger = logger,
            FileSystem = fileSystem
        };
        var currentState = SpecState.CreateSpecState(entity.CurrentStateName);

        if (!currentState.UpdateSpecField(specContext, fieldName, value))
        {
            throw new InvalidOperationException($"Cannot set field '{fieldName}' on spec at state '{currentState.Name}'.");
        }

        EpicMapper.SyncSpecToEntity(specContext.Spec, entity);
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
                .Append(epic.EpicAgentName)
                .Distinct()
                .Select(id => new AgentAgreement { AgentId = id })
                .ToList()
        };

        EpicMapper.SyncToEntity(epic, entity);

        entity.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicSwarmRaised,
            epicState: entity.CurrentStateName,
            epicId: epicId,
            actor: entity.EpicAgentName,
            message: new { agents = epic.AgentSwarm!.Agreements.Select(a => a.AgentId).ToList() }
        ));

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

        entity.CurrentStateName = Lifecycles.EpicStates.HumanInLoopState.StateName;
        entity.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicHumanLoop,
            epicState: entity.CurrentStateName,
            epicId: epicId,
            actor: entity.EpicAgentName,
            message: new { question = request.Questions, approveToState = request.ApproveToStateName, rejectToState = request.RejectToStateName }
        ));

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

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicHumanLoopResolved,
            epicState: entity.CurrentStateName,
            epicId: epicId,
            actor: "human",
            message: new { approved = request.IsApproved, note = request.HumanInput }
        ));

        await db.SaveChangesAsync(cancellationToken);

        var decision = request.IsApproved ? "approved" : "rejected";
        var note = string.IsNullOrWhiteSpace(request.HumanInput) ? "" : $" Note: {request.HumanInput}";
        await tmux.SendKeys(entity.EpicAgentName, $"Human {decision} epic {epicId}.{note} Call advance(\"{epicId}\") to continue.", cancellationToken);
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

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicSwarmVote,
            epicState: entity.CurrentStateName,
            epicId: epicId,
            actor: request.AgentId,
            message: new { agent = request.AgentId, hasAgreed = request.HasAgreed, note = request.Note }
        ));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WakeAgent(string epicId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);

        var message = entity.CurrentStateName == DraftingState.StateName
            ? $"Work on epic \"{entity.Id}\". Call get_epic(\"{entity.Id}\") to read the current state and instruction, then call advance(\"{entity.Id}\") to begin."
            : $"Continue epic \"{entity.Id}\". Call get_epic(\"{entity.Id}\") to read the current state and instruction, then call advance(\"{entity.Id}\") to proceed.";

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicNudged,
            epicState: entity.CurrentStateName,
            epicId: epicId,
            actor: "human"
        ));

        await db.SaveChangesAsync(cancellationToken);

        await tmux.SendKeys(entity.EpicAgentName, message, cancellationToken);
    }

    public async Task DeleteEpic(string epicId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindEpicOrThrow(epicId, cancellationToken);
        await broker.DeleteChannel($"epic-{epicId}", entity.EpicAgentName, cancellationToken);
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

        var epicContext = new EpicContext { Epic = epic, Logger = logger, FileSystem = fileSystem, Options = options.Value, Broker = broker };

        var currentState = EpicState.CreateEpicState(epic.CurrentStateName);
        string previousName;

        do
        {
            previousName = currentState.Name;
            currentState = await currentState.MoveNext(epicContext, cancellationToken);
            epic.CurrentStateName = currentState.Name;
        } while (currentState.Name != previousName);

        if (fromState == Lifecycles.EpicStates.HumanInLoopState.StateName)
        {
            epic.PrependHumanNote();
        }

        entity.CurrentStateName = epic.CurrentStateName;
        entity.UpdatedAt = DateTime.UtcNow;

        EpicMapper.SyncToEntity(epic, entity);

        foreach (var spec in epic.Specs)
        {
            var specEntity = entity.Specs.First(s => s.Id == spec.Id);
            EpicMapper.SyncSpecToEntity(spec, specEntity);
            specEntity.UpdatedAt = DateTime.UtcNow;
        }

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.EpicMoveNext,
            epicState: epic.CurrentStateName,
            epicId: epicId,
            actor: request.EpicAgentId,
            message: new { from = fromState, to = epic.CurrentStateName, instruction = epic.EpicAgentInstruction }
        ));

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
            IsCodeReviewRequired = request.IsCodeReviewRequired,
            IsACRequired = request.IsACRequired,
            SpecDocPath = request.SpecDocPath,
            DependsOn = request.DependsOn is { Count: > 0 }
                ? JsonSerializer.Serialize(request.DependsOn)
                : null,
            CurrentStateName = DraftingSpecState.StateName,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Specs.Add(entity);

        var epicEntity = await db.FindEpicOrThrow(epicId, cancellationToken);

        if (string.IsNullOrEmpty(entity.ReviewerAgentName) && !string.IsNullOrEmpty(epicEntity.ReviewerAgentName))
        {
            entity.ReviewerAgentName = epicEntity.ReviewerAgentName;
        }

        entity.IsACRequired ??= epicEntity.IsACRequired;
        entity.IsCodeReviewRequired ??= epicEntity.IsCodeReviewRequired;

        if (!string.IsNullOrEmpty(entity.SpecDocPath) && fileSystem.FileExists(entity.SpecDocPath))
        {
            entity.IsSpecDrafted = true;
        }

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.SpecCreated,
            epicState: epicEntity.CurrentStateName,
            epicId: epicId,
            specId: slug,
            specState: entity.CurrentStateName,
            actor: request.AssignedAgentName,
            message: new { specName = request.SpecName, agent = request.AssignedAgentName }
        ));

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
        var epicEntity = await db.FindEpicOrThrow(entity.EpicId, cancellationToken);

        entity.AssignedAgentName = spec.AssignedAgentName;
        entity.SpecDocPath = spec.SpecDocPath;
        entity.IsSpecDrafted = spec.IsSpecDrafted;
        entity.IsCodeDone = spec.IsCodeDone;
        entity.IsCodeReviewApproved = spec.IsCodeReviewApproved;
        entity.IsAcPassed = spec.IsAcPassed;
        entity.IsACRequired = spec.IsACRequired;
        entity.IsCodeReviewRequired = spec.IsCodeReviewRequired;
        entity.ReviewerAgentName = spec.ReviewerAgentName;
        entity.DependsOn = spec.DependsOn.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(spec.DependsOn) : null;
        entity.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.SpecUpdated,
            epicState: epicEntity.CurrentStateName,
            epicId: entity.EpicId,
            specId: spec.Id,
            specState: entity.CurrentStateName,
            actor: "human",
            message: new { specId = spec.Id }
        ));

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

        var epicEntity = await db.FindEpicOrThrow(entity.EpicId, cancellationToken);

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.SpecHumanLoopResolved,
            epicState: epicEntity.CurrentStateName,
            epicId: entity.EpicId,
            specId: specId,
            specState: entity.CurrentStateName,
            actor: "human",
            message: new { approved = request.IsApproved, note = request.HumanInput }
        ));

        await db.SaveChangesAsync(cancellationToken);

        await tmux.SendKeys(epicEntity.EpicAgentName, $"Human {(request.IsApproved ? "approved" : "rejected")} spec {specId}. Call advance_spec(\"{specId}\") then advance(\"{epicEntity.Id}\").", cancellationToken);
    }

    public async Task<Spec> MarkSpecReadyToCode(string specId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);

        entity.IsReadyToCode = true;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var spec = await AdvanceSpec(specId, cancellationToken);

        var epicEntity = await db.FindEpicOrThrow(entity.EpicId, cancellationToken);
        await tmux.SendKeys(
            epicEntity.EpicAgentName,
            $"Spec {specId} is ready to code. Call advance_spec(\"{specId}\") to begin.",
            cancellationToken
        );

        return spec;
    }

    public async Task<Spec> ForceSpecState(string specId, string stateName, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);
        var epicEntity = await db.FindEpicOrThrow(entity.EpicId, cancellationToken);
        var fromState = entity.CurrentStateName;
        entity.CurrentStateName = stateName;
        entity.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.SpecForceState,
            epicState: epicEntity.CurrentStateName,
            epicId: entity.EpicId,
            specId: specId,
            specState: stateName,
            actor: "human",
            message: new { from = fromState, to = stateName }
        ));
        await db.SaveChangesAsync(cancellationToken);
        return EpicMapper.ToSpec(entity);
    }

    public async Task<Spec> AbandonSpec(string specId, bool abandon, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);
        var epicEntity = await db.FindEpicOrThrow(entity.EpicId, cancellationToken);

        entity.IsAbandoned = abandon;
        entity.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.SpecUpdated,
            epicState: epicEntity.CurrentStateName,
            epicId: entity.EpicId,
            specId: specId,
            specState: entity.CurrentStateName,
            actor: "human",
            message: new { specId, abandoned = abandon }
        ));

        await db.SaveChangesAsync(cancellationToken);

        return EpicMapper.ToSpec(entity);
    }

    public async Task<Spec> FlagScopeChange(string specId, FlagScopeChangeRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);
        var spec = EpicMapper.ToSpec(entity);

        spec.FlagScopeChange(request.Description);

        EpicMapper.SyncSpecToEntity(spec, entity);
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return spec;
    }

    public async Task<Spec> ApproveScopeChange(string specId, ApproveScopeChangeRequest request, CancellationToken cancellationToken = default)
    {
        var specEntity = await db.FindSpecOrThrow(specId, cancellationToken);
        var epicEntity = await db.FindEpicOrThrow(specEntity.EpicId, cancellationToken);
        var spec = EpicMapper.ToSpec(specEntity);

        if (spec.ScopeChange is null)
        {
            throw new InvalidOperationException($"No active ScopeChange for spec {specId}.");
        }

        spec.ResolveScopeChange(request.IsApproved, request.HumanNote);

        EpicMapper.SyncSpecToEntity(spec, specEntity);
        specEntity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var decision = request.IsApproved ? "approved" : "rejected";
        await tmux.SendKeys(epicEntity.EpicAgentName, $"Scope change {decision} for spec {specId}. Call advance_spec(\"{specId}\") to continue.", cancellationToken);

        return await AdvanceSpec(specId, cancellationToken);
    }

    /// <summary>
    /// Advances the spec lifecycle one step. Called by the Epic Agent via MCP.
    /// </summary>
    public async Task<Spec> AdvanceSpec(string specId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FindSpecOrThrow(specId, cancellationToken);
        var epicEntity = await db.FindEpicOrThrow(entity.EpicId, cancellationToken);

        var spec = EpicMapper.ToSpec(entity);

        var specContext = new SpecContext
        {
            Spec = spec,
            Epic = ToEpic(epicEntity),
            Logger = logger,
            FileSystem = fileSystem
        };

        var fromSpecState = spec.CurrentStateName;
        var currentState = SpecState.CreateSpecState(spec.CurrentStateName);
        string previousName;

        do
        {
            previousName = currentState.Name;
            currentState = await currentState.MoveNext(specContext, cancellationToken);
            spec.CurrentStateName = currentState.Name;
        } while (currentState.Name != previousName);

        if (fromSpecState == Lifecycles.SpecStates.HumanInLoopSpecState.StateName)
        {
            spec.PrependHumanNote();
        }

        EpicMapper.SyncSpecToEntity(spec, entity);

        entity.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(EpicMapper.MakeAuditLog(
            action: AuditAction.SpecMoveNext,
            epicState: epicEntity.CurrentStateName,
            epicId: entity.EpicId,
            specId: specId,
            specState: spec.CurrentStateName,
            message: new { from = fromSpecState, to = spec.CurrentStateName }
        ));

        await db.SaveChangesAsync(cancellationToken);

        return spec;
    }


}

