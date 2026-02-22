using Microsoft.Extensions.Options;
using ReplayFilesViewApi.Models;

namespace ReplayFilesViewApi.Services;

public interface IProjectService
{
    List<ProjectSettings> GetAll();
    ProjectSettings? GetBySlug(string slug);
}

public class ProjectService : IProjectService
{
    private readonly List<ProjectSettings> _projects;

    public ProjectService(IOptions<List<ProjectSettings>> projects)
    {
        _projects = projects.Value;
    }

    public List<ProjectSettings> GetAll() => _projects;

    public ProjectSettings? GetBySlug(string slug)
    {
        return _projects.FirstOrDefault(p =>
            string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }
}
