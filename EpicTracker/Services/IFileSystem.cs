namespace EpicTracker.Services;

public interface IFileSystem
{
    bool FileExists(string? path);
    void CreateDirectory(string path);
    void CopyFile(string sourcePath, string destPath, bool overwrite = false);
}

public class FileSystem : IFileSystem
{
    public bool FileExists(string? path) => path is not null && File.Exists(Path.GetFullPath(path));
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void CopyFile(string sourcePath, string destPath, bool overwrite = false) => File.Copy(sourcePath, destPath, overwrite);
}
