using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReplayFilesViewApi.Models;

namespace ReplayFilesViewApi.Services;

public interface IProjectService
{
    List<ProjectSettings> GetAll();
    ProjectSettings? GetBySlug(string slug);
    void AddProject(ProjectSettings project);
    void UpdateProject(ProjectSettings project);
    void DeleteProject(string slug);
}

public class ProjectService : IProjectService
{
    private readonly string _projectsFilePath;
    private readonly ILogger<ProjectService> _logger;
    private List<ProjectSettings> _projects = new();
    private readonly object _lock = new();

    public ProjectService(IWebHostEnvironment env, ILogger<ProjectService> logger)
    {
        _logger = logger;
        // Store projects.json in the content root (persistent across deploys if mounted)
        _projectsFilePath = Path.Combine(env.ContentRootPath, "projects.json");
        LoadProjects();
    }

    private void LoadProjects()
    {
        lock (_lock)
        {
            if (File.Exists(_projectsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_projectsFilePath);
                    _projects = JsonSerializer.Deserialize<List<ProjectSettings>>(json) ?? new();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading projects from {Path}", _projectsFilePath);
                    _projects = new();
                }
            }
            else
            {
                _projects = new();
                SaveProjects();
            }
        }
    }

    private void SaveProjects()
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_projects, options);
                File.WriteAllText(_projectsFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving projects to {Path}", _projectsFilePath);
            }
        }
    }

    public List<ProjectSettings> GetAll() => _projects;

    public ProjectSettings? GetBySlug(string slug)
    {
        return _projects.FirstOrDefault(p =>
            string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    public void AddProject(ProjectSettings project)
    {
        lock (_lock)
        {
            if (_projects.Any(p => p.Slug.Equals(project.Slug, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Project with slug '{project.Slug}' already exists.");

            _projects.Add(project);
            SaveProjects();
        }
    }

    public void UpdateProject(ProjectSettings project)
    {
        lock (_lock)
        {
            var index = _projects.FindIndex(p => p.Slug.Equals(project.Slug, StringComparison.OrdinalIgnoreCase));
            if (index == -1)
                throw new InvalidOperationException($"Project with slug '{project.Slug}' not found.");

            _projects[index] = project;
            SaveProjects();
        }
    }

    public void DeleteProject(string slug)
    {
        lock (_lock)
        {
            var removed = _projects.RemoveAll(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                SaveProjects();
        }
    }
}
