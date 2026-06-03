namespace EpicTracker.Contracts;

public class HumanInLoop
{
    public string Questions { get; set; } = default!;
    public string? HumanInput { get; set; }
    public bool? IsApproved { get; set; }
    public string ApproveToStateName { get; set; } = default!;
    public string RejectToStateName { get; set; } = default!;

    public void SetResumeToState(string stateName)
    {
        ApproveToStateName = stateName;
        RejectToStateName = stateName;
    }
}
