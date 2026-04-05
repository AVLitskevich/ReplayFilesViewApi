using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ReplayFilesViewApi.Models;
using ReplayFilesViewApi.Services;

var builder = WebApplication.CreateSlimBuilder(args);

// Load external appsettings.json if ROOT_PATH is provided
var rootPath = Environment.GetEnvironmentVariable("ROOT_PATH");
if (!string.IsNullOrEmpty(rootPath))
{
    var externalConfigPath = Path.Combine(rootPath, "replay-data", "appsettings.json");
    builder.Configuration.AddJsonFile(externalConfigPath, optional: true, reloadOnChange: true);
}

// Configure project settings
builder.Services.Configure<AdminSettings>(
    builder.Configuration.GetSection("AdminSettings"));

// Register services
builder.Services.AddSingleton<IProjectService, ProjectService>();
builder.Services.AddSingleton<IReplayFileService, ReplayFileService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<ISystemService, SystemService>();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login.html";
        options.LogoutPath = "/api/admin/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

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
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Rate limiting
app.UseRateLimiter();

// Static files
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// API endpoints
var api = app.MapGroup("/api");

// GET /api/projects - list all projects
api.MapGet("/projects", (IProjectService projectService) =>
{
    var projects = projectService.GetAll()
        .Select(p => new ProjectInfo(p.Slug, p.Name, p.Description, p.WebGLUrl, string.IsNullOrEmpty(p.ReplayViewerUrl) ? null : p.ReplayViewerUrl))
        .ToList();
    return Results.Ok(new ProjectListResponse(projects));
}).WithName("GetProjects");

// GET /api/projects/{slug} - get single project
api.MapGet("/projects/{slug}", (string slug, IProjectService projectService) =>
{
    var project = projectService.GetBySlug(slug);
    if (project == null) return Results.NotFound();

    return Results.Ok(new ProjectInfo(project.Slug, project.Name, project.Description, project.WebGLUrl, string.IsNullOrEmpty(project.ReplayViewerUrl) ? null : project.ReplayViewerUrl));
}).WithName("GetProject");

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

// HTML play page: /{slug}/play
app.MapGet("/{slug}/play", (string slug, IProjectService projectService) =>
{
    var project = projectService.GetBySlug(slug);
    if (project == null) return Results.NotFound();

    return Results.File("play.html", "text/html");
}).WithName("ProjectPlayPage");

// HTML viewer page: /{slug}/viewer
app.MapGet("/{slug}/viewer", (string slug, IProjectService projectService) =>
{
    var project = projectService.GetBySlug(slug);
    if (project == null) return Results.NotFound();

    return Results.File("viewer.html", "text/html");
}).WithName("ProjectViewerPage");

// Cleaner URLs for Login and Admin
app.MapGet("/login", () => Results.File("login.html", "text/html"));
app.MapGet("/admin", () => Results.File("admin.html", "text/html"));

// Admin API endpoints
var adminApi = app.MapGroup("/api/admin").RequireAuthorization();

// Login Status (Public)
app.MapGet("/api/admin/status", (HttpContext httpContext) => 
{
    return Results.Ok(new { isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false });
}).AllowAnonymous();

// Login (Public)
app.MapPost("/api/admin/login", async (LoginRequest request, IAuthService authService, Microsoft.Extensions.Options.IOptions<AdminSettings> adminSettings, HttpContext httpContext) =>
{
    if (authService.VerifyPassword(request.Password, adminSettings.Value) && 
        string.Equals(request.Username, adminSettings.Value.Username, StringComparison.OrdinalIgnoreCase))
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, request.Username) };
        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
        return Results.Ok();
    }
    return Results.Unauthorized();
}).AllowAnonymous();

// Logout
adminApi.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

// GET /api/admin/projects - list all projects (including config)
adminApi.MapGet("/projects", (IProjectService projectService) =>
{
    return Results.Ok(projectService.GetAll());
});

// POST /api/admin/projects - create new project
adminApi.MapPost("/projects", (ProjectSettings project, IProjectService projectService) =>
{
    try {
        projectService.AddProject(project);
        return Results.Created($"/api/projects/{project.Slug}", project);
    } catch (Exception ex) {
        return Results.BadRequest(ex.Message);
    }
});

// PUT /api/admin/projects/{slug} - update project
adminApi.MapPut("/projects/{slug}", (string slug, ProjectSettings project, IProjectService projectService) =>
{
    try {
        if (!slug.Equals(project.Slug, StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("Slug mismatch.");
        projectService.UpdateProject(project);
        return Results.Ok(project);
    } catch (Exception ex) {
        return Results.NotFound(ex.Message);
    }
});

// DELETE /api/admin/projects/{slug} - delete project
adminApi.MapDelete("/projects/{slug}", (string slug, IProjectService projectService) =>
{
    projectService.DeleteProject(slug);
    return Results.NoContent();
});

// POST /api/admin/projects/{slug}/restart - restart game server
adminApi.MapPost("/projects/{slug}/restart", async (string slug, IProjectService projectService, ISystemService systemService) =>
{
    var project = projectService.GetBySlug(slug);
    if (project == null) return Results.NotFound();

    var (success, output) = await systemService.RestartGame(project);
    return success ? Results.Ok(output) : Results.BadRequest(output);
});

// GET /api/admin/projects/{slug}/logs - stream logs (SSE)
adminApi.MapGet("/projects/{slug}/logs", async (string slug, IProjectService projectService, ISystemService systemService, HttpContext httpContext, CancellationToken ct) =>
{
    var project = projectService.GetBySlug(slug);
    if (project == null) {
        httpContext.Response.StatusCode = 404;
        return;
    }

    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    await foreach (var line in systemService.StreamLogs(project, ct))
    {
        await httpContext.Response.WriteAsync($"data: {line}\n\n", ct);
        await httpContext.Response.Body.FlushAsync(ct);
    }
});

app.Run();

public record LoginRequest(string Username, string Password);

[JsonSerializable(typeof(ProjectListResponse))]
[JsonSerializable(typeof(ProjectInfo))]
[JsonSerializable(typeof(List<ProjectInfo>))]
[JsonSerializable(typeof(ReplayListResponse))]
[JsonSerializable(typeof(ReplayFileInfo))]
[JsonSerializable(typeof(List<ReplayFileInfo>))]
[JsonSerializable(typeof(ProjectSettings))]
[JsonSerializable(typeof(List<ProjectSettings>))]
[JsonSerializable(typeof(LoginRequest))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
