using EpicTracker.Contracts;
using System.Text.Json;

namespace EpicTracker.Data;

internal static class EpicMapper
{
    internal static Epic ToEpic(EpicEntity entity)
    {
        var epic = new Epic
        {
            Id = entity.Id,
            Name = entity.Name,
            EpicAgent = entity.EpicAgent,
            Brief = entity.Brief,
            Slug = entity.Slug,
            NeedsMockup = entity.NeedsMockup,
            IsDocDrafted = entity.IsDocDrafted,
            MockupPath = entity.MockupPath,
            IsMockupDone = entity.IsMockupDone,
            ReviewerAgentId = entity.ReviewerAgentId,
            CreatedAt = entity.CreatedAt,
            CodingAgents = JsonSerializer.Deserialize<List<string>>(entity.CodingAgents) ?? [],
            CurrentStateName = entity.CurrentStateName,
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
        entity.NeedsMockup = epic.NeedsMockup;
        entity.IsDocDrafted = epic.IsDocDrafted;
        entity.MockupPath = epic.MockupPath;
        entity.IsMockupDone = epic.IsMockupDone;
entity.ReviewerAgentId = epic.ReviewerAgentId;

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
            AssignedAgentId = entity.AssignedAgentId,
            ReviewerAgentId = entity.ReviewerAgentId,
            CodeReviewRequired = entity.CodeReviewRequired,
            SpecDocPath = entity.SpecDocPath,
            IsSpecApproved = entity.IsSpecApproved,
            IsAbandoned = entity.IsAbandoned,
            IsSpecDrafted = entity.IsSpecDrafted,
            IsAcPassed = entity.IsAcPassed,
            IsCodeDone = entity.IsCodeDone,
            IsCodeReviewApproved = entity.IsCodeReviewApproved,
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

        return spec;
    }

    internal static void SyncSpecToEntity(Spec spec, SpecEntity entity)
    {
        entity.CurrentStateName = spec.CurrentStateName;
        entity.EpicAgentInstruction = spec.EpicAgentInstruction;
        entity.IsSpecApproved = spec.IsSpecApproved;
        entity.IsAbandoned = spec.IsAbandoned;
        entity.IsSpecDrafted = spec.IsSpecDrafted;
        entity.IsCodeDone = spec.IsCodeDone;
        entity.IsCodeReviewApproved = spec.IsCodeReviewApproved;
        entity.IsAcPassed = spec.IsAcPassed;

        entity.HumanInLoop = spec.HumanInLoop is not null
            ? JsonSerializer.Serialize(spec.HumanInLoop)
            : null;

        entity.AgentSwarm = spec.AgentSwarm is not null
            ? JsonSerializer.Serialize(spec.AgentSwarm)
            : null;
    }

    internal static EpicAudit ToEpicAudit(EpicAuditEntity entity)
    {
        return new EpicAudit
        {
            Id = entity.Id,
            EpicId = entity.EpicId,
            EpicAgentId = entity.EpicAgentId,
            FromState = entity.FromState,
            ToState = entity.ToState,
            EpicAgentInstruction = entity.EpicAgentInstruction,
            Timestamp = entity.Timestamp
        };
    }

    internal static EpicAuditEntity ToAudit(string epicId, string epicAgentId, string fromState, Epic epic)
    {
        return new EpicAuditEntity
        {
            EpicId = epicId,
            EpicAgentId = epicAgentId,
            FromState = fromState,
            ToState = epic.CurrentStateName,
            EpicAgentInstruction = epic.EpicAgentInstruction,
            Timestamp = DateTime.UtcNow,
            HumanInLoop = epic.HumanInLoop is not null
                ? JsonSerializer.Serialize(epic.HumanInLoop)
                : null,
            AgentSwarm = epic.AgentSwarm is not null
                ? JsonSerializer.Serialize(epic.AgentSwarm)
                : null
        };
    }
}

