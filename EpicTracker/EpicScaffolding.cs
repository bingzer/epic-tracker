using EpicTracker.Contracts;
using EpicTracker.Services;

namespace EpicTracker;

public class EpicScaffolding(IFileSystem fileSystem)
{
    public void Scaffold(Epic epic, string governanceTemplatePath)
    {
        var epicRoot = Path.Combine(epic.BasePath, "epics", epic.Slug);

        fileSystem.CreateDirectory(epicRoot);
        fileSystem.CreateDirectory(epic.SpecsDirectory);
        fileSystem.CreateDirectory(epic.MockupDirectory);
        fileSystem.CreateDirectory(Path.Combine(epicRoot, "output"));

        fileSystem.CopyFile(governanceTemplatePath, epic.EpicGovernancePath, overwrite: true);
    }
}
