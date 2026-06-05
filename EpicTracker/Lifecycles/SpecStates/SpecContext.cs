using EpicTracker.Services;
using Microsoft.Extensions.Logging;

namespace EpicTracker.Lifecycles.SpecStates;

internal class SpecContext
{
    public required Spec Spec { get; init; }
    public required Epic Epic { get; init; }
    public required ILogger Logger { get; init; }
    public required IFileSystem FileSystem { get; init; }

    public bool IsACRequired => Spec.IsACRequired ?? Epic.IsACRequired;
    public bool IsCodeReviewRequired => Spec.IsCodeReviewRequired ?? Epic.IsCodeReviewRequired;
}
