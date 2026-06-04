namespace EpicTracker.Services;

public interface IFileSystem
{
    bool FileExists(string? path);
}

public class FileSystem : IFileSystem
{
    public bool FileExists(string? path) => path is not null && File.Exists(Path.GetFullPath(path));
}
