using System.Text.Json.Serialization;
using ReplayFilesViewApi.Models;
using ReplayFilesViewApi.Services;

var builder = WebApplication.CreateSlimBuilder(args);

// Настройка конфигурации ReplaySettings с переопределением через переменную окружения
builder.Services.Configure<ReplaySettings>(options =>
{
    builder.Configuration.GetSection("ReplaySettings").Bind(options);

    // Переопределение пути через переменную окружения
    var envFolderPath = Environment.GetEnvironmentVariable("REPLAY_FOLDER_PATH");
    if (!string.IsNullOrWhiteSpace(envFolderPath))
    {
        options.FolderPath = envFolderPath;
    }
});

// Регистрация сервиса для работы с файлами реплеев
builder.Services.AddSingleton<IReplayFileService, ReplayFileService>();

// Настройка JSON сериализации
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Настройка статических файлов
app.UseDefaultFiles();
app.UseStaticFiles();

// API эндпоинты
var api = app.MapGroup("/api");

// GET /api/replays - получение списка реплеев
api.MapGet("/replays", (IReplayFileService replayService) =>
{
    var replays = replayService.GetReplays();
    return Results.Ok(new ReplayListResponse(replays));
})
.WithName("GetReplays")
.WithOpenApi();

// GET /api/replays/{fileName} - скачивание файла реплея
api.MapGet("/replays/{fileName}", (string fileName, IReplayFileService replayService) =>
{
    var (isValid, fullPath) = replayService.ValidateAndGetFilePath(fileName);

    if (!isValid || fullPath == null)
    {
        return Results.NotFound();
    }

    var fileStream = File.OpenRead(fullPath);
    return Results.File(fileStream, "application/octet-stream", fileName);
})
.WithName("DownloadReplay")
.WithOpenApi();

app.Run();

[JsonSerializable(typeof(ReplayListResponse))]
[JsonSerializable(typeof(ReplayFileInfo))]
[JsonSerializable(typeof(List<ReplayFileInfo>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
