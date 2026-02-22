using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ReplayFilesViewApi.Models;
using ReplayFilesViewApi.Services;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure project settings
builder.Services.Configure<List<ProjectSettings>>(
    builder.Configuration.GetSection("Projects"));

// Register services
builder.Services.AddSingleton<IProjectService, ProjectService>();
builder.Services.AddSingleton<IReplayFileService, ReplayFileService>();

// Rate limiting: 30 requests per minute per IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Rate limiting
app.UseRateLimiter();

// Static files
app.UseDefaultFiles();
app.UseStaticFiles();

// API endpoints
var api = app.MapGroup("/api");

// GET /api/projects - list all projects
api.MapGet("/projects", (IProjectService projectService) =>
{
    var projects = projectService.GetAll()
        .Select(p => new ProjectInfo(p.Slug, p.Name, p.Description, p.WebGLUrl))
        .ToList();
    return Results.Ok(new ProjectListResponse(projects));
}).WithName("GetProjects");

// GET /api/projects/{slug}/replays - list project replays
api.MapGet("/projects/{slug}/replays", (string slug, IProjectService projectService, IReplayFileService replayService) =>
{
    var project = projectService.GetBySlug(slug);
    if (project == null) return Results.NotFound();

    var replays = replayService.GetReplays(project);
    return Results.Ok(new ReplayListResponse(replays));
}).WithName("GetProjectReplays");

// GET /api/projects/{slug}/replays/{fileName} - download replay file
api.MapGet("/projects/{slug}/replays/{fileName}", (string slug, string fileName, IProjectService projectService, IReplayFileService replayService) =>
{
    var project = projectService.GetBySlug(slug);
    if (project == null) return Results.NotFound();

    var (isValid, fullPath) = replayService.ValidateAndGetFilePath(project, fileName);
    if (!isValid || fullPath == null) return Results.NotFound();

    var fileStream = File.OpenRead(fullPath);
    return Results.File(fileStream, "application/octet-stream", fileName);
}).WithName("DownloadReplay");

// HTML replay page: /{slug}/replays
app.MapGet("/{slug}/replays", (string slug, IProjectService projectService) =>
{
    var project = projectService.GetBySlug(slug);
    if (project == null) return Results.NotFound();

    return Results.File("replays.html", "text/html");
}).WithName("ProjectReplaysPage");

app.Run();

[JsonSerializable(typeof(ProjectListResponse))]
[JsonSerializable(typeof(ProjectInfo))]
[JsonSerializable(typeof(List<ProjectInfo>))]
[JsonSerializable(typeof(ReplayListResponse))]
[JsonSerializable(typeof(ReplayFileInfo))]
[JsonSerializable(typeof(List<ReplayFileInfo>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
