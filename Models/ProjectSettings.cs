namespace ReplayFilesViewApi.Models;

public enum RestartType
{
    None = 0,
    Systemd = 1,
    DockerCompose = 2
}

public enum LogType
{
    Standard = 0, // stdout/stderr (e.g. docker compose logs)
    File = 1      // specific file (e.g. tail -f)
}

public class ProjectSettings
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string WebGLUrl { get; set; } = "";
    public string ReplayViewerUrl { get; set; } = "";
    public string ReplayFolderPath { get; set; } = "";
    public string FileExtension { get; set; } = ".replay";
    
    // Restart Configuration
    public RestartType RestartType { get; set; } = RestartType.None;
    public string RestartServiceName { get; set; } = "";
    public string RestartPath { get; set; } = ""; // Path to docker-compose.yml or script
    
    // Log Configuration
    public LogType LogType { get; set; } = LogType.Standard;
    public string LogPath { get; set; } = ""; // Internal container path or host path

    // Local Build Paths (serve WebGL builds directly instead of via nginx)
    public string ClientBuildPath { get; set; } = "";
    public string ReplayViewerBuildPath { get; set; } = "";

    // If set, the game is a Unity WebGL build and will be loaded directly via Unity loader API
    // Value is the Unity product name used in build file names (e.g. "Game-PixelDash-Web")
    public string? UnityProductName { get; set; }
}
