namespace ReplayFilesViewApi.Services;

public interface IWebGLBuildService
{
    (bool IsValid, string? FullPath) ValidateAndGetBuildFilePath(string baseBuildPath, string requestedPath);
    (string ContentType, string? ContentEncoding) GetContentTypeAndEncoding(string filePath);
    bool BuildPathExists(string buildPath);
}

public class WebGLBuildService : IWebGLBuildService
{
    private readonly ILogger<WebGLBuildService> _logger;

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Web
        { ".html", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },

        // WebGL / Unity
        { ".wasm", "application/wasm" },
        { ".data", "application/octet-stream" },
        { ".unityweb", "application/octet-stream" },

        // Images
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },
        { ".webp", "image/webp" },

        // Fonts
        { ".woff", "font/woff" },
        { ".woff2", "font/woff2" },
        { ".ttf", "font/ttf" },

        // Other
        { ".txt", "text/plain" },
        { ".map", "application/json" },
    };

    public WebGLBuildService(ILogger<WebGLBuildService> logger)
    {
        _logger = logger;
    }

    public bool BuildPathExists(string buildPath)
    {
        return !string.IsNullOrEmpty(buildPath) && Directory.Exists(buildPath);
    }

    public (bool IsValid, string? FullPath) ValidateAndGetBuildFilePath(string baseBuildPath, string requestedPath)
    {
        if (string.IsNullOrEmpty(requestedPath))
            return (false, null);

        var fullPath = Path.Combine(baseBuildPath, requestedPath);
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedBasePath = Path.GetFullPath(baseBuildPath);

        if (!normalizedFullPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected: {RequestedPath}", requestedPath);
            return (false, null);
        }

        if (!File.Exists(normalizedFullPath))
            return (false, null);

        return (true, normalizedFullPath);
    }

    public (string ContentType, string? ContentEncoding) GetContentTypeAndEncoding(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        // Handle compressed files: .wasm.br, .data.gz, etc.
        if (extension.Equals(".br", StringComparison.OrdinalIgnoreCase))
        {
            var innerExtension = Path.GetExtension(Path.GetFileNameWithoutExtension(filePath));
            var contentType = GetMimeType(innerExtension);
            return (contentType, "br");
        }

        if (extension.Equals(".gz", StringComparison.OrdinalIgnoreCase))
        {
            var innerExtension = Path.GetExtension(Path.GetFileNameWithoutExtension(filePath));
            var contentType = GetMimeType(innerExtension);
            return (contentType, "gzip");
        }

        return (GetMimeType(extension), null);
    }

    private static string GetMimeType(string extension)
    {
        return MimeTypes.TryGetValue(extension, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }
}
