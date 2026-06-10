using EpicTracker.Contracts;
using System.Text.Json;

namespace EpicTracker.Data;

internal static class EpicMapper
{
    internal static Epic ToEpic(EpicEntity entity, string basePath = "")
    {
        var epic = new Epic
        {
            Id = entity.Id,
            Name = entity.Name,
            EpicAgentName = entity.EpicAgentName,
            Brief = entity.Brief,
            Slug = entity.Slug,
            BasePath = basePath,
            NeedsMockup = entity.NeedsMockup,
            IsDocDrafted = entity.IsDocDrafted,
            IsMockupDone = entity.IsMockupDone,
            IsBriefRefined = entity.IsBriefRefined,
            IsACRequired = entity.IsACRequired,
            IsCodeReviewRequired = entity.IsCodeReviewRequired,
            WaterproofingIterations = entity.WaterproofingIterations,
            ReviewerAgentName = entity.ReviewerAgentName,
            CreatedAt = entity.CreatedAt,
            CodingAgentNames = JsonSerializer.Deserialize<List<string>>(entity.CodingAgentNames) ?? [],
            CurrentStateName = entity.CurrentStateName,
            LastKnownStateName = entity.LastKnownStateName,
            Specs = entity.Specs.Select(ToSpec).ToList()
        };

        if (entity.HumanInLoop is not null)
        {
            epic.HumanInLoop = JsonSerializer.Deserialize<HumanInLoop>(entity.HumanInLoop);
        }

        if (entity.AgentSwarm is not null)
        {
            epic.AgentSwarm = JsonSerializer.Deserialize<AgentSwarm>(entity.AgentSwarm);
        }

        return epic;
    }

    // Writes live HumanInLoop, AgentSwarm, and typed flag state back to the entity after a transition.
    internal static void SyncToEntity(Epic epic, EpicEntity entity)
    {
        entity.Name = epic.Name;
        entity.EpicAgentName = epic.EpicAgentName;
        entity.Brief = epic.Brief;
        entity.CodingAgentNames = JsonSerializer.Serialize(epic.CodingAgentNames);
        entity.IsBriefRefined = epic.IsBriefRefined;
        entity.NeedsMockup = epic.NeedsMockup;
        entity.IsDocDrafted = epic.IsDocDrafted;
        entity.IsMockupDone = epic.IsMockupDone;
        entity.IsACRequired = epic.IsACRequired;
        entity.IsCodeReviewRequired = epic.IsCodeReviewRequired;
        entity.WaterproofingIterations = epic.WaterproofingIterations;
        entity.ReviewerAgentName = epic.ReviewerAgentName;

        entity.LastKnownStateName = epic.LastKnownStateName;

        entity.HumanInLoop = epic.HumanInLoop is not null
            ? JsonSerializer.Serialize(epic.HumanInLoop)
            : null;

        entity.AgentSwarm = epic.AgentSwarm is not null
            ? JsonSerializer.Serialize(epic.AgentSwarm)
            : null;
    }

    internal static Spec ToSpec(SpecEntity entity)
    {
        var spec = new Spec
        {
            Id = entity.Id,
            EpicId = entity.EpicId,
            AssignedAgentName = entity.AssignedAgentName,
            ReviewerAgentName = entity.ReviewerAgentName,
            IsACRequired = entity.IsACRequired,
            IsCodeReviewRequired = entity.IsCodeReviewRequired,
            SpecDocPath = entity.SpecDocPath,
            IsSpecApproved = entity.IsSpecApproved,
            IsAbandoned = entity.IsAbandoned,
            IsSpecDrafted = entity.IsSpecDrafted,
            IsAcPassed = entity.IsAcPassed,
            IsReadyToCode = entity.IsReadyToCode,
            IsCodeDone = entity.IsCodeDone,
            IsCodeReviewApproved = entity.IsCodeReviewApproved,
            CodeReviewIterations = entity.CodeReviewIterations,
            CurrentStateName = entity.CurrentStateName
        };

        if (entity.EpicAgentInstruction is not null)
        {
            spec.SetEpicAgentInstruction(entity.EpicAgentInstruction);
        }

        if (entity.HumanInLoop is not null)
        {
            spec.HumanInLoop = JsonSerializer.Deserialize<HumanInLoop>(entity.HumanInLoop);
        }

        if (entity.AgentSwarm is not null)
        {
            spec.AgentSwarm = JsonSerializer.Deserialize<AgentSwarm>(entity.AgentSwarm);
        }

        if (entity.ScopeChange is not null)
        {
            spec.ScopeChange = JsonSerializer.Deserialize<ScopeChange>(entity.ScopeChange);
        }

        return spec;
    }

    internal static void SyncSpecToEntity(Spec spec, SpecEntity entity)
    {
        entity.CurrentStateName = spec.CurrentStateName;
        entity.EpicAgentInstruction = spec.EpicAgentInstruction;
        entity.IsSpecApproved = spec.IsSpecApproved;
        entity.IsAbandoned = spec.IsAbandoned;
        entity.IsSpecDrafted = spec.IsSpecDrafted;
        entity.IsReadyToCode = spec.IsReadyToCode;
        entity.IsCodeDone = spec.IsCodeDone;
        entity.IsCodeReviewApproved = spec.IsCodeReviewApproved;
        entity.CodeReviewIterations = spec.CodeReviewIterations;
        entity.IsAcPassed = spec.IsAcPassed;
        entity.IsACRequired = spec.IsACRequired;
        entity.IsCodeReviewRequired = spec.IsCodeReviewRequired;

        entity.HumanInLoop = spec.HumanInLoop is not null
            ? JsonSerializer.Serialize(spec.HumanInLoop)
            : null;

        entity.AgentSwarm = spec.AgentSwarm is not null
            ? JsonSerializer.Serialize(spec.AgentSwarm)
            : null;

        entity.ScopeChange = spec.ScopeChange is not null
            ? JsonSerializer.Serialize(spec.ScopeChange)
            : null;
    }

    internal static AuditLog ToAuditLog(AuditLogEntity entity)
    {
        return new AuditLog
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            Action = entity.Action,
            EpicState = entity.EpicState,
            SpecState = entity.SpecState,
            EpicId = entity.EpicId,
            SpecId = entity.SpecId,
            Actor = entity.Actor,
            Message = entity.Message
        };
    }

    internal static AuditLogEntity MakeAuditLog(string action, string epicState, string epicId, string? specId = null, string? specState = null, string? actor = null, object? message = null)
    {
        return new AuditLogEntity
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            EpicState = epicState,
            SpecState = specState,
            EpicId = epicId,
            SpecId = specId,
            Actor = actor,
            Message = message is not null ? JsonSerializer.Serialize(message) : null
        };
    }
}

