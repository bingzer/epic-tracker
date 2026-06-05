namespace EpicTracker;

public class EpicTrackerOptions
{
    public string EpicsBasePath { get; set; } = default!;
    public string GovernanceTemplatePath { get; set; } = default!;
    public int MaxWaterproofingIterations { get; set; } = 5;
}
