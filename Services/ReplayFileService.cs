using System.Text.RegularExpressions;
using ReplayFilesViewApi.Models;

namespace ReplayFilesViewApi.Services;

public interface IReplayFileService
{
    List<ReplayFileInfo> GetReplays(ProjectSettings project);
    (bool IsValid, string? FullPath) ValidateAndGetFilePath(ProjectSettings project, string fileName);
}

public partial class ReplayFileService : IReplayFileService
{
    private readonly ILogger<ReplayFileService> _logger;

    [GeneratedRegex(@"^[a-zA-Z0-9_\-\.]+$")]
    private static partial Regex FileNameValidationRegex();

    [GeneratedRegex(@"replay_(\d{4})-(\d{2})-(\d{2})_(\d{2})-(\d{2})-(\d{2})")]
    private static partial Regex DateParseRegex();

    public ReplayFileService(ILogger<ReplayFileService> logger)
    {
        _logger = logger;
    }

    public List<ReplayFileInfo> GetReplays(ProjectSettings project)
    {
        try
        {
            if (!Directory.Exists(project.ReplayFolderPath))
            {
                _logger.LogWarning("Replay folder does not exist: {FolderPath}", project.ReplayFolderPath);
                return new List<ReplayFileInfo>();
            }

            var files = Directory.GetFiles(project.ReplayFolderPath, $"*{project.FileExtension}");

            var replays = files
                .Select(filePath =>
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileInfo = new FileInfo(filePath);
                    var date = ParseDateFromFileName(project.ReplayFolderPath, fileName);
                    var displayName = CreateDisplayName(date);

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
            _logger.LogError(ex, "Error reading replay files from {FolderPath}", project.ReplayFolderPath);
            return new List<ReplayFileInfo>();
        }
    }

    public (bool IsValid, string? FullPath) ValidateAndGetFilePath(ProjectSettings project, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !FileNameValidationRegex().IsMatch(fileName))
        {
            _logger.LogWarning("Invalid file name format: {FileName}", fileName);
            return (false, null);
        }

        if (!fileName.EndsWith(project.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid file extension: {FileName}", fileName);
            return (false, null);
        }

        var fullPath = Path.Combine(project.ReplayFolderPath, fileName);

        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedBasePath = Path.GetFullPath(project.ReplayFolderPath);

        if (!normalizedFullPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected: {FileName}", fileName);
            return (false, null);
        }

        if (!File.Exists(normalizedFullPath))
        {
            _logger.LogWarning("File not found: {FileName}", fileName);
            return (false, null);
        }

        return (true, normalizedFullPath);
    }

    private DateTime ParseDateFromFileName(string folderPath, string fileName)
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

        var filePath = Path.Combine(folderPath, fileName);
        if (File.Exists(filePath))
        {
            return File.GetLastWriteTimeUtc(filePath);
        }

        return DateTime.UtcNow;
    }

    private static string CreateDisplayName(DateTime date)
    {
        return $"Replay {date:dd.MM.yyyy HH:mm:ss} UTC";
    }
}
