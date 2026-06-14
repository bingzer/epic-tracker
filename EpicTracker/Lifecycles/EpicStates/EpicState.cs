using System.Reflection;
using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.EpicStates;

/// <summary>
/// Base class for all epic lifecycle states.
/// </summary>
/// <remarks>
/// <para>
/// <b>States are stateless.</b> All mutable data lives on <see cref="Epic"/>. Never store fields on a state.
/// </para>
/// <para>
/// <b>Return <c>this</c></b> to signal "blocked, waiting for external input."
/// The Epic Agent reads <c>Epic.EpicAgentInstruction</c>, acts, then calls Advance again.
/// </para>
///
/// <b>AgentSwarm pattern — mandatory vs optional:</b>
/// <list type="bullet">
///   <item>
///     <b>Mandatory swarm:</b> The state enforces consensus as a gate condition.
///     If <c>epic.AgentSwarm is null</c>, set instruction telling the Epic Agent to call
///     <c>RaiseAgentSwarm</c> with the objective and <c>toStateName</c>, then <c>return this</c>.
///     If swarm exists but no consensus yet, <c>return new AgentSwarmState()</c>.
///     Only proceed when <c>epic.AgentSwarm.HasConsensus</c>.
///     <code>
///     if (epic.AgentSwarm is null) { /* instruct agent to raise swarm */ return this; }
///     if (!epic.AgentSwarm.HasConsensus) { return new AgentSwarmState(); }
///     // consensus confirmed — proceed
///     </code>
///   </item>
///   <item>
///     <b>Optional swarm:</b> The state does not check for a swarm at all.
///     The Epic Agent may call <c>RaiseAgentSwarm</c> anytime it feels uncertain,
///     but the state will not block on it. Example: DraftingState.
///   </item>
/// </list>
///
/// <b>HumanInLoop pattern:</b>
/// Same principle — states that require human approval check <c>epic.HumanInLoop</c> as a gate.
/// The Epic Agent calls <c>RaiseHumanInLoop</c> with <c>approveToStateName</c> / <c>rejectToStateName</c>,
/// then calls Advance. <c>HumanInLoopState</c> routes based on <c>IsApproved</c>.
/// The UI calls <c>ApproveHumanInLoop</c> to record the human's decision.
/// </remarks>
internal abstract class EpicState
{
    public abstract string Name { get; }

    protected abstract Task<EpicState> Next(EpicContext context, CancellationToken cancellationToken = default);

    public async Task<EpicState> MoveNext(EpicContext context, CancellationToken cancellationToken = default)
    {
        var next = await Next(context, cancellationToken);

        context.Logger.LogInformation(
            "[Epic {EpicId}] {FromState} → {ToState} | {Instruction}",
            context.Epic.Id,
            Name,
            next.Name,
            context.Epic.EpicAgentInstruction
        );

        return next;
    }

    public bool UpdateEpicField(EpicContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Epic.Name))
        {
            context.Epic.Name = value;
            return true;
        }

        if (fieldName == nameof(Epic.EpicAgentName))
        {
            context.Epic.EpicAgentName = value;
            return true;
        }

        if (fieldName == nameof(Epic.CodingAgentNames))
        {
            context.Epic.CodingAgentNames = value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            return true;
        }

        if (fieldName == nameof(Epic.SpecWritingPhase))
        {
            context.Epic.SpecWritingPhase = int.Parse(value);
            return true;
        }

        return UpdateEpicFieldAt(context, fieldName, value);
    }

    protected virtual bool UpdateEpicFieldAt(EpicContext context, string fieldName, string value) => false;

    protected EpicState Exit(EpicContext context, string instruction)
    {
        context.Epic.SetEpicAgentInstruction(instruction);

        return this;
    }

    protected AgentSwarmState RaiseAgentSwarm(EpicContext context, string objective, string whenApprovedStateName, string instruction)
    {
        context.Epic.RaiseAgentSwarm(
            objective: objective,
            toStateName: whenApprovedStateName,
            instruction: instruction
        );

        return new AgentSwarmState();
    }

    protected HumanInLoopState RaiseHumanInLoop(EpicContext context, string questions, string approveToStateName, string rejectToStateName, string instruction)
    {
        context.Epic.RaiseHumanInLoop(
            questions: questions,
            approveToStateName: approveToStateName,
            rejectToStateName: rejectToStateName,
            instruction: instruction
        );

        return new HumanInLoopState();
    }

    protected EpicState MoveTo(string stateName)
    {
        return CreateEpicState(stateName);
    }

    private static readonly Dictionary<string, Func<EpicState>> Factories = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.IsSubclassOf(typeof(EpicState)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
        .ToDictionary(
            t => ((EpicState)Activator.CreateInstance(t)!).Name,
            t => (Func<EpicState>)(() => (EpicState)Activator.CreateInstance(t)!)
        );

    internal static EpicState CreateEpicState(string stateName)
    {
        if (!Factories.TryGetValue(stateName, out var factory))
        {
            throw new InvalidOperationException($"Unknown state: {stateName}");
        }

        return factory();
    }
}

