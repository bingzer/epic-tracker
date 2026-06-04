namespace EpicTracker.Contracts;

public record AdvanceEpicRequest(string EpicAgentId);

public record ApproveEpicHumanInLoopRequest(bool IsApproved, string? HumanInput);

public record RaiseAgentSwarmRequest(string Objective, string ToStateName);

public record RaiseHumanInLoopRequest(string Questions, string ApproveToStateName, string RejectToStateName);

public record SubmitAgreementRequest(string AgentId, bool HasAgreed, string? Note);

public record CreateSpecRequest(
    string SpecName,
    string AssignedAgentId,
    string? SpecDocPath,
    bool CodeReviewRequired,
    string? ReviewerAgentId);

public record ApproveSpecHumanInLoopRequest(bool IsApproved, string? HumanInput);

public record CreateEpicRequest(
    string EpicAgent,
    string Brief,
    string? Name,
    List<string>? CodingAgents,
    bool NeedsMockup,
    string? ReviewerAgentId);

public record ForceSpecStateRequest(string StateName);

public record ForceEpicStateRequest(string StateName);
