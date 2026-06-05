namespace EpicTracker.Contracts;

public class Spec
{
    public string Id { get; set; } = default!;
    public string EpicId { get; set; } = default!;
    public string AssignedAgentName { get; set; } = default!;
    public string? ReviewerAgentName { get; set; }
    public bool? IsACRequired { get; set; }
    public bool? IsCodeReviewRequired { get; set; }
    public string? SpecDocPath { get; set; }
    public bool IsSpecApproved { get; set; }
    public bool IsAbandoned { get; set; }
    public bool IsSpecDrafted { get; set; }
    public bool? IsAcPassed { get; set; }
    public bool IsReadyToCode { get; set; }
    public bool IsCodeDone { get; set; }
    public bool? IsCodeReviewApproved { get; set; }

    public string CurrentStateName { get; set; } = default!;
    public string? EpicAgentInstruction { get; private set; }
    public HumanInLoop? HumanInLoop { get; set; }
    public AgentSwarm? AgentSwarm { get; set; }

    public void SetEpicAgentInstruction(string instruction)
    {
        EpicAgentInstruction = instruction;
    }

    public bool IsHumanApproved() => HumanInLoop?.IsApproved == true;
    public bool IsHumanRejected() => HumanInLoop?.IsApproved == false;
    public bool NeedsHumanReview() => HumanInLoop is null;

    public void PrependHumanNote()
    {
        if (HumanInLoop?.HumanInput is not { Length: > 0 } note) return;
        if (EpicAgentInstruction is null) return;

        var decision = HumanInLoop.IsApproved == true ? "approved" : "rejected";
        SetEpicAgentInstruction($"Human {decision}. Their note: \"{note}\"\n\n{EpicAgentInstruction}");
    }

    public void ResetHumanApproval()
    {
        HumanInLoop = null;
    }

    public void RaiseHumanInLoop(string questions, string approveToStateName, string rejectToStateName, string instruction)
    {
        HumanInLoop = new HumanInLoop
        {
            Questions = questions,
            ApproveToStateName = approveToStateName,
            RejectToStateName = rejectToStateName
        };

        SetEpicAgentInstruction(instruction);
    }

}
