using System.Reflection;

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

    public abstract Task<SpecState> MoveNext(Spec spec, CancellationToken cancellationToken = default);

    private static readonly Dictionary<string, Func<SpecState>> Factories = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.IsSubclassOf(typeof(SpecState)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
        .ToDictionary(
            t => ((SpecState)Activator.CreateInstance(t)!).Name,
            t => (Func<SpecState>)(() => (SpecState)Activator.CreateInstance(t)!)
        );

    internal static SpecState Create(string stateName)
    {
        if (!Factories.TryGetValue(stateName, out var factory))
        {
            throw new InvalidOperationException($"Unknown spec state: {stateName}");
        }

        return factory();
    }
}
