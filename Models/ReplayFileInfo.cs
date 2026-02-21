namespace ReplayFilesViewApi.Models;

public record ReplayFileInfo(
    string FileName,
    string DisplayName,
    DateTime Date,
    long SizeBytes
);
