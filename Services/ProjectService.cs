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
    private readonly IOptionsMonitor<List<ProjectSettings>> _monitor;

    public ProjectService(IOptionsMonitor<List<ProjectSettings>> monitor)
    {
        _monitor = monitor;
    }

    public List<ProjectSettings> GetAll() => _monitor.CurrentValue;

    public ProjectSettings? GetBySlug(string slug)
    {
        return _monitor.CurrentValue.FirstOrDefault(p =>
            string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }
}
