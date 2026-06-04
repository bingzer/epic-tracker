using EpicTracker.Services;
using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.SpecStates;

internal class SpecContext
{
    public required Spec Spec { get; init; }
    public required ILogger Logger { get; init; }
    public required IFileSystem FileSystem { get; init; }
}
