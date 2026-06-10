namespace EpicTracker.Contracts;

public record AdvanceEpicRequest(string EpicAgentId);

public record ApproveEpicHumanInLoopRequest(bool IsApproved, string? HumanInput);

public record RaiseAgentSwarmRequest(string Objective, string ToStateName);

public record RaiseHumanInLoopRequest(string Questions, string ApproveToStateName, string RejectToStateName);

public record SubmitAgreementRequest(string AgentId, bool HasAgreed, string? Note);

public record CreateSpecRequest(
    string SpecName,
    string AssignedAgentName,
    string? SpecDocPath,
    bool? IsCodeReviewRequired,
    string? ReviewerAgentName,
    List<string>? DependsOn = null);

public record ApproveSpecHumanInLoopRequest(bool IsApproved, string? HumanInput);

public record CreateEpicRequest(
    string EpicAgentName,
    string Brief,
    string? Name,
    List<string>? CodingAgentNames,
    bool NeedsMockup,
    string? ReviewerAgentName,
    bool IsACRequired = true,
    bool? IsCodeReviewRequired = null);

public record ForceSpecStateRequest(string StateName);

public record ForceEpicStateRequest(string StateName);

public record FlagScopeChangeRequest(string Description);

public record ApproveScopeChangeRequest(bool IsApproved, string? HumanNote);
