using EpicTracker.Services;
using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.EpicStates;

internal class EpicContext
{
    public required Epic Epic { get; init; }
    public required ILogger Logger { get; init; }
    public required IFileSystem FileSystem { get; init; }
}
