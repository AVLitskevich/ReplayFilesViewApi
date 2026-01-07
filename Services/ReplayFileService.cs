using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ReplayFilesViewApi.Models;

namespace ReplayFilesViewApi.Services;

public interface IReplayFileService
{
    List<ReplayFileInfo> GetReplays();
    (bool IsValid, string? FullPath) ValidateAndGetFilePath(string fileName);
}

public partial class ReplayFileService : IReplayFileService
{
    private readonly ReplaySettings _settings;
    private readonly ILogger<ReplayFileService> _logger;

    [GeneratedRegex(@"^[a-zA-Z0-9_\-\.]+$")]
    private static partial Regex FileNameValidationRegex();

    [GeneratedRegex(@"replay_(\d{4})-(\d{2})-(\d{2})_(\d{2})-(\d{2})-(\d{2})")]
    private static partial Regex DateParseRegex();

    public ReplayFileService(IOptions<ReplaySettings> settings, ILogger<ReplayFileService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public List<ReplayFileInfo> GetReplays()
    {
        try
        {
            if (!Directory.Exists(_settings.FolderPath))
            {
                _logger.LogWarning("Replay folder does not exist: {FolderPath}", _settings.FolderPath);
                return new List<ReplayFileInfo>();
            }

            var files = Directory.GetFiles(_settings.FolderPath, $"*{_settings.FileExtension}");

            var replays = files
                .Select(filePath =>
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileInfo = new FileInfo(filePath);
                    var date = ParseDateFromFileName(fileName);
                    var displayName = CreateDisplayName(fileName, date);

                    return new ReplayFileInfo(
                        FileName: fileName,
                        DisplayName: displayName,
                        Date: date,
                        SizeBytes: fileInfo.Length
                    );
                })
                .OrderByDescending(r => r.Date)
                .ToList();

            return replays;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading replay files from {FolderPath}", _settings.FolderPath);
            return new List<ReplayFileInfo>();
        }
    }

    public (bool IsValid, string? FullPath) ValidateAndGetFilePath(string fileName)
    {
        // Валидация имени файла - только безопасные символы
        if (string.IsNullOrWhiteSpace(fileName) || !FileNameValidationRegex().IsMatch(fileName))
        {
            _logger.LogWarning("Invalid file name format: {FileName}", fileName);
            return (false, null);
        }

        // Проверка расширения
        if (!fileName.EndsWith(_settings.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid file extension: {FileName}", fileName);
            return (false, null);
        }

        var fullPath = Path.Combine(_settings.FolderPath, fileName);

        // Защита от path traversal - проверяем, что файл находится внутри разрешенной папки
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedBasePath = Path.GetFullPath(_settings.FolderPath);

        if (!normalizedFullPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected: {FileName}", fileName);
            return (false, null);
        }

        // Проверяем существование файла
        if (!File.Exists(normalizedFullPath))
        {
            _logger.LogWarning("File not found: {FileName}", fileName);
            return (false, null);
        }

        return (true, normalizedFullPath);
    }

    private DateTime ParseDateFromFileName(string fileName)
    {
        var match = DateParseRegex().Match(fileName);

        if (match.Success)
        {
            try
            {
                var year = int.Parse(match.Groups[1].Value);
                var month = int.Parse(match.Groups[2].Value);
                var day = int.Parse(match.Groups[3].Value);
                var hour = int.Parse(match.Groups[4].Value);
                var minute = int.Parse(match.Groups[5].Value);
                var second = int.Parse(match.Groups[6].Value);

                return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing date from file name: {FileName}", fileName);
            }
        }

        // Если не удалось распарсить дату из имени, используем дату модификации файла
        var filePath = Path.Combine(_settings.FolderPath, fileName);
        if (File.Exists(filePath))
        {
            return File.GetLastWriteTimeUtc(filePath);
        }

        return DateTime.UtcNow;
    }

    private string CreateDisplayName(string fileName, DateTime date)
    {
        // Формат: "Реплей 07.01.2025 14:30:00 UTC"
        return $"Реплей {date:dd.MM.yyyy HH:mm:ss} UTC";
    }
}
