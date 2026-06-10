namespace EpicTracker.Contracts;

public class ScopeChange
{
    public string Description { get; set; } = default!;
    public bool? IsApproved { get; set; }
    public string? HumanNote { get; set; }
}
