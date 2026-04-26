namespace KF.Scripts.Models;

/// <summary>Represents a script stored in the registry.</summary>
public sealed record ScriptRecord(
    long ScriptId,
    string ApplicationId,
    string Name,
    string TypeTag,
    string Language,
    string Content,
    string? Description,
    bool IsEnabled,
    string CreatedBy,
    DateTime CreatedDate,
    string ModifiedBy,
    DateTime ModifiedDate,
    string? Comment,
    byte[] RowVersion);
