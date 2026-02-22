namespace ReplayFilesViewApi.Models;

public class ProjectSettings
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string WebGLUrl { get; set; } = "";
    public string ReplayFolderPath { get; set; } = "";
    public string FileExtension { get; set; } = ".replay";
}
