using System.Reflection;
using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.SpecStates;

/// <summary>
/// Base class for all spec lifecycle states. Same pattern as EpicState — see EpicState.cs for full pattern docs.
/// </summary>
/// <remarks>
/// States are stateless. All mutable data lives on <see cref="Spec"/>.
/// Return <c>this</c> to signal blocked. Epic Agent reads <c>Spec.EpicAgentInstruction</c>, acts, calls AdvanceSpec again.
/// </remarks>
internal abstract class SpecState
{
    public abstract string Name { get; }

    protected abstract Task<SpecState> Next(SpecContext context, CancellationToken cancellationToken = default);

    public async Task<SpecState> MoveNext(SpecContext context, CancellationToken cancellationToken = default)
    {
        var next = await Next(context, cancellationToken);

        context.Logger.LogInformation(
            "[Spec {SpecId}] {FromState} → {ToState} | {Instruction}",
            context.Spec.Id,
            Name,
            next.Name,
            context.Spec.EpicAgentInstruction
        );

        return next;
    }

    public bool UpdateSpecField(SpecContext context, string fieldName, string value)
    {
        if (fieldName == nameof(Spec.AssignedAgentName))
        {
            context.Spec.AssignedAgentName = value;
            return true;
        }
        
        if (fieldName == nameof(Spec.ReviewerAgentName))
        {
            context.Spec.ReviewerAgentName = value;
            return true;
        }

        if (fieldName == nameof(Spec.IsCodeReviewRequired))
        {
            context.Spec.IsCodeReviewRequired = bool.Parse(value);
            return true;
        }

        if (fieldName == nameof(Spec.SpecDocPath))
        {
            if (!Path.IsPathRooted(value))
            {
                throw new InvalidOperationException($"SpecDocPath must be an absolute path. Got: '{value}'");
            }
            context.Spec.SpecDocPath = value;
            return true;
        }

        return UpdateSpecFieldAt(context, fieldName, value);
    }

    protected virtual bool UpdateSpecFieldAt(SpecContext context, string fieldName, string value) => false;

    protected SpecState Exit(SpecContext context, string instruction)
    {
        context.Spec.SetEpicAgentInstruction(instruction);
        return this;
    }

    protected HumanInLoopSpecState RaiseHumanInLoop(SpecContext context, string questions, string approveToStateName, string rejectToStateName, string instruction)
    {
        context.Spec.RaiseHumanInLoop(
            questions: questions,
            approveToStateName: approveToStateName,
            rejectToStateName: rejectToStateName,
            instruction: instruction
        );
        return new HumanInLoopSpecState();
    }

    protected SpecState MoveTo(string stateName) => CreateSpecState(stateName);

    private static readonly Dictionary<string, Func<SpecState>> Factories = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.IsSubclassOf(typeof(SpecState)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
        .ToDictionary(
            t => ((SpecState)Activator.CreateInstance(t)!).Name,
            t => (Func<SpecState>)(() => (SpecState)Activator.CreateInstance(t)!)
        );

    internal static SpecState CreateSpecState(string stateName)
    {
        if (!Factories.TryGetValue(stateName, out var factory))
        {
            throw new InvalidOperationException($"Unknown spec state: {stateName}");
        }

        return factory();
    }
}
