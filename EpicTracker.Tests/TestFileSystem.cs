using EpicTracker.Services;

namespace EpicTracker.Tests;

internal class TestFileSystem : IFileSystem
{
    private readonly bool _defaultExists;
    private readonly HashSet<string> _overrides = new(StringComparer.OrdinalIgnoreCase);

    public TestFileSystem(bool defaultExists = true)
    {
        _defaultExists = defaultExists;
    }

    public void AddExistingPath(string path) => _overrides.Add(path);

    public bool FileExists(string? path)
    {
        if (path is null) return false;
        if (_overrides.Contains(path)) return !_defaultExists;
        return _defaultExists;
    }
}
