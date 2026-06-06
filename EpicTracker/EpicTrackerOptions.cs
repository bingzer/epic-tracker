namespace EpicTracker;

public class EpicTrackerOptions
{
    public string EpicsBasePath { get; set; } = default!;
    public string GovernanceTemplatePath { get; set; } = default!;
    public string TmuxBrokerUrl { get; set; } = "http://127.0.0.1:6792";
    public int MaxWaterproofingIterations { get; set; } = 5;
}
