namespace EpicTracker;

public static class AuditAction
{
    public const string EpicCreated = "epic.created";
    public const string EpicMoveNext = "epic.move.next";
    public const string EpicHumanLoop = "epic.human.loop";
    public const string EpicHumanLoopResolved = "epic.human.loop.resolved";
    public const string EpicSwarmRaised = "epic.swarm.raised";
    public const string EpicSwarmVote = "epic.swarm.vote";
    public const string EpicNudged = "epic.nudged";
    public const string EpicForceState = "epic.force.state";
    public const string EpicUpdated = "epic.updated";
    public const string SpecCreated = "spec.created";
    public const string SpecUpdated = "spec.updated";
    public const string SpecMoveNext = "spec.move.next";
    public const string SpecForceState = "spec.force.state";
    public const string SpecHumanLoopResolved = "spec.human.loop.resolved";
}
