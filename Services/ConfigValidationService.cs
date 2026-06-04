using System.Diagnostics;
using System.Text.RegularExpressions;
using ReplayFilesViewApi.Models;

namespace ReplayFilesViewApi.Services;

public interface IConfigValidationService
{
    Task<ConfigValidationResponse> ValidateAsync(ProjectSettings project, string baseUrl, CancellationToken ct);
}

public partial class ConfigValidationService : IConfigValidationService
{
    private readonly IWebGLBuildService _buildService;
    private readonly ILogger<ConfigValidationService> _logger;
    private readonly HttpClient _httpClient;

    [GeneratedRegex(@"^[a-z0-9\-]+$")]
    private static partial Regex SlugRegex();

    public ConfigValidationService(IWebGLBuildService buildService, ILogger<ConfigValidationService> logger)
    {
        _buildService = buildService;
        _logger = logger;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            // We only care whether the resource exists, not cert validity of the upstream.
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
    }

    public async Task<ConfigValidationResponse> ValidateAsync(ProjectSettings project, string baseUrl, CancellationToken ct)
    {
        var fields = new List<FieldValidation>();

        // slug
        if (string.IsNullOrWhiteSpace(project.Slug))
            fields.Add(new("slug", FieldStatus.Error, "Slug обязателен."));
        else if (!SlugRegex().IsMatch(project.Slug))
            fields.Add(new("slug", FieldStatus.Error, "Только строчные буквы, цифры и дефис (a-z, 0-9, -)."));
        else
            fields.Add(new("slug", FieldStatus.Ok, "OK"));

        // name
        if (string.IsNullOrWhiteSpace(project.Name))
            fields.Add(new("name", FieldStatus.Error, "Название обязательно."));
        else
            fields.Add(new("name", FieldStatus.Ok, "OK"));

        // replayFolderPath
        if (!string.IsNullOrWhiteSpace(project.ReplayFolderPath))
        {
            fields.Add(Directory.Exists(project.ReplayFolderPath)
                ? new("replayFolderPath", FieldStatus.Ok, "Папка найдена.")
                : new("replayFolderPath", FieldStatus.Error, "Папка не найдена на сервере."));
        }

        // fileExtension
        if (!string.IsNullOrWhiteSpace(project.FileExtension))
        {
            fields.Add(project.FileExtension.StartsWith('.')
                ? new("fileExtension", FieldStatus.Ok, "OK")
                : new("fileExtension", FieldStatus.Warning, "Обычно начинается с точки (например .replay)."));
        }

        // clientBuildPath
        if (!string.IsNullOrWhiteSpace(project.ClientBuildPath))
            fields.Add(ValidateBuildPath("clientBuildPath", project.ClientBuildPath));

        // replayViewerBuildPath
        if (!string.IsNullOrWhiteSpace(project.ReplayViewerBuildPath))
            fields.Add(ValidateBuildPath("replayViewerBuildPath", project.ReplayViewerBuildPath));

        // unityProductName — only meaningful with an existing client build
        if (!string.IsNullOrWhiteSpace(project.UnityProductName))
        {
            if (_buildService.BuildPathExists(project.ClientBuildPath))
                fields.Add(ValidateUnityLoader(project.ClientBuildPath, project.UnityProductName));
            // if no client build, leave unverified (no entry)
        }

        // webGLUrl
        if (!string.IsNullOrWhiteSpace(project.WebGLUrl))
            fields.Add(await ValidateUrlAsync("webGLUrl", project.WebGLUrl, baseUrl, ct));

        // replayViewerUrl
        if (!string.IsNullOrWhiteSpace(project.ReplayViewerUrl))
            fields.Add(await ValidateUrlAsync("replayViewerUrl", project.ReplayViewerUrl, baseUrl, ct));

        // restartServiceName — only when restart is configured
        if (project.RestartType != RestartType.None)
        {
            if (string.IsNullOrWhiteSpace(project.RestartServiceName))
                fields.Add(new("restartServiceName", FieldStatus.Error, "Имя сервиса обязательно при выбранном протоколе рестарта."));
            else
                fields.Add(await ValidateServiceAsync(project, ct));
        }

        // restartPath
        if (!string.IsNullOrWhiteSpace(project.RestartPath))
        {
            fields.Add(File.Exists(project.RestartPath)
                ? new("restartPath", FieldStatus.Ok, "Файл найден.")
                : new("restartPath", FieldStatus.Error, "Файл не найден на сервере."));
        }

        // logPath — host path (volume mounted into the game container), only when LogType=File
        if (project.LogType == LogType.File && !string.IsNullOrWhiteSpace(project.LogPath))
        {
            fields.Add(File.Exists(project.LogPath)
                ? new("logPath", FieldStatus.Ok, "Файл лога найден.")
                : new("logPath", FieldStatus.Error, "Файл лога не найден на сервере."));
        }

        var errorCount = fields.Count(f => f.Status == FieldStatus.Error);
        var warningCount = fields.Count(f => f.Status == FieldStatus.Warning);

        return new ConfigValidationResponse(errorCount == 0, errorCount, warningCount, fields);
    }

    private FieldValidation ValidateBuildPath(string field, string buildPath)
    {
        if (!_buildService.BuildPathExists(buildPath))
            return new(field, FieldStatus.Error, "Папка билда не найдена на сервере.");

        var indexPath = Path.Combine(buildPath, "index.html");
        return File.Exists(indexPath)
            ? new(field, FieldStatus.Ok, "Папка билда и index.html найдены.")
            : new(field, FieldStatus.Warning, "Папка есть, но index.html внутри не найден.");
    }

    private FieldValidation ValidateUnityLoader(string clientBuildPath, string productName)
    {
        var buildDir = Path.Combine(clientBuildPath, "Build");
        if (!Directory.Exists(buildDir))
            return new("unityProductName", FieldStatus.Warning, "Папка Build/ не найдена в билде — не удалось проверить loader.");

        try
        {
            var loaders = Directory.GetFiles(buildDir, "*.loader.js");
            var match = loaders.Any(f =>
                Path.GetFileName(f).Contains(productName, StringComparison.OrdinalIgnoreCase));
            if (match)
                return new("unityProductName", FieldStatus.Ok, "Unity loader найден.");

            var available = loaders.Length > 0
                ? " Найдено: " + string.Join(", ", loaders.Select(Path.GetFileName))
                : "";
            return new("unityProductName", FieldStatus.Warning,
                $"Не найден loader, совпадающий с '{productName}'.{available}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error inspecting Unity build dir {Dir}", buildDir);
            return new("unityProductName", FieldStatus.Warning, "Не удалось прочитать папку Build/.");
        }
    }

    private async Task<FieldValidation> ValidateUrlAsync(string field, string value, string baseUrl, CancellationToken ct)
    {
        string absolute;
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            absolute = value;
        }
        else
        {
            absolute = baseUrl.TrimEnd('/') + "/" + value.TrimStart('/');
        }

        if (!Uri.TryCreate(absolute, UriKind.Absolute, out var uri))
            return new(field, FieldStatus.Error, $"Некорректный URL: {absolute}");

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await SendAsync(uri, HttpMethod.Head, timeoutCts.Token);

            // Some servers reject HEAD — retry with GET.
            if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed ||
                response.StatusCode == System.Net.HttpStatusCode.NotImplemented)
            {
                response.Dispose();
                response = await SendAsync(uri, HttpMethod.Get, timeoutCts.Token);
            }

            using (response)
            {
                var code = (int)response.StatusCode;
                if (code == 404)
                    return new(field, FieldStatus.Error, $"404 при запросе {absolute}");
                if (response.IsSuccessStatusCode)
                    return new(field, FieldStatus.Ok, $"Доступен ({code}).");
                return new(field, FieldStatus.Warning, $"Ответ {code} при запросе {absolute}");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(field, FieldStatus.Warning, $"Таймаут при запросе {absolute}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "URL validation failed for {Url}", absolute);
            return new(field, FieldStatus.Warning, $"Не удалось проверить ({ex.Message}).");
        }
    }

    private Task<HttpResponseMessage> SendAsync(Uri uri, HttpMethod method, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, uri);
        return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task<FieldValidation> ValidateServiceAsync(ProjectSettings project, CancellationToken ct)
    {
        if (project.RestartType == RestartType.DockerCompose)
        {
            var (ok, _) = await RunOnceAsync("docker", ["inspect", project.RestartServiceName], ct);
            return ok
                ? new("restartServiceName", FieldStatus.Ok, "Docker-контейнер найден.")
                : new("restartServiceName", FieldStatus.Error, $"Docker-контейнер '{project.RestartServiceName}' не найден.");
        }
        else // Systemd
        {
            var unit = project.RestartServiceName.Contains('.')
                ? project.RestartServiceName
                : project.RestartServiceName + ".service";
            var (ok, output) = await RunOnceAsync("systemctl", ["show", unit, "-p", "LoadState", "--value"], ct);
            var loadState = output.Trim();
            if (ok && string.Equals(loadState, "loaded", StringComparison.OrdinalIgnoreCase))
                return new("restartServiceName", FieldStatus.Ok, "Systemd-юнит найден.");
            return new("restartServiceName", FieldStatus.Error, $"Systemd-юнит '{unit}' не найден (LoadState={loadState}).");
        }
    }

    // Mirrors SystemService.RunOnceAsync — kept local to avoid touching the existing service.
    private async Task<(bool success, string output)> RunOnceAsync(string fileName, string[] arguments, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            foreach (var a in arguments)
                process.StartInfo.ArgumentList.Add(a);

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var stdout = await outputTask;
            var stderr = await errorTask;

            if (process.ExitCode == 0)
                return (true, stdout);
            return (false, stderr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Validation command failed to run: {FileName}", fileName);
            return (false, ex.Message);
        }
    }
}
