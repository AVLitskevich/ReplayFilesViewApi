namespace ReplayFilesViewApi.Models;

public enum FieldStatus
{
    Ok,
    Warning,
    Error,
    Skipped
}

public record FieldValidation(string Field, FieldStatus Status, string Message);

public record ConfigValidationResponse(
    bool IsValid,       // true if no Error entries
    int ErrorCount,
    int WarningCount,
    List<FieldValidation> Fields
);
