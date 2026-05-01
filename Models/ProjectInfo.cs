namespace ReplayFilesViewApi.Models;

public record ProjectInfo(
    string Slug,
    string Name,
    string Description,
    string WebGLUrl,
    string? ReplayViewerUrl,
    string? ClientBuildUrl,
    string? ViewerBuildUrl,
    string? UnityProductName
);

public record ProjectListResponse(
    List<ProjectInfo> Projects
);
