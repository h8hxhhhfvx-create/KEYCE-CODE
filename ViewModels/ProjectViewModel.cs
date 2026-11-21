using KeyceCode.Models;
using KeyceCode.Services;

namespace KeyceCode.ViewModels;

public sealed class ProjectViewModel(FileService fileService, ProjectService projectService)
{
    public Project? CurrentProject { get; private set; }

    public Project OpenProject(string path)
    {
        CurrentProject = projectService.Open(path);
        return CurrentProject;
    }

    public CodeFile CreateFile(string parentDirectory, string name)
    {
        var file = fileService.CreateFile(parentDirectory, name);
        Refresh();
        return file;
    }

    public string CreateDirectory(string parentDirectory, string name)
    {
        var path = fileService.CreateDirectory(parentDirectory, name);
        Refresh();
        return path;
    }

    public string Rename(string path, string newName)
    {
        var newPath = fileService.Rename(path, newName);
        Refresh();
        return newPath;
    }

    public void Delete(string path, bool recursive)
    {
        fileService.Delete(path, recursive);
        Refresh();
    }

    public void Refresh()
    {
        if (CurrentProject is null) return;
        projectService.Refresh(CurrentProject);
    }
}
