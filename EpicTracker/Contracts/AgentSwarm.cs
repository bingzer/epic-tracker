namespace EpicTracker.Contracts;

public class AgentAgreement
{
    public string AgentId { get; set; } = default!;
    public bool? HasAgreed { get; set; }
    public string? Note { get; set; }
}

public class AgentSwarm
{
    public string Objective { get; set; } = default!;
    public string? HumanInput { get; set; }
    public int Iteration { get; set; }
    public List<AgentAgreement> Agreements { get; set; } = [];
    public string ToStateName { get; set; } = default!;
    public Dictionary<string, string>? AgentDomainFocus { get; set; }
    public bool KickoffPosted { get; set; }

    public bool HasConsensus => Agreements.Count > 0 && Agreements.All(a => a.HasAgreed == true);
    public bool HasDisagreement => Agreements.Any(a => a.HasAgreed == false);
    public bool IsComplete => Agreements.All(a => a.HasAgreed.HasValue);
}
