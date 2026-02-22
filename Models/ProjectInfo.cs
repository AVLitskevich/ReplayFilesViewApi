namespace ReplayFilesViewApi.Models;

public record ProjectInfo(
    string Slug,
    string Name,
    string Description,
    string WebGLUrl
);

public record ProjectListResponse(
    List<ProjectInfo> Projects
);
