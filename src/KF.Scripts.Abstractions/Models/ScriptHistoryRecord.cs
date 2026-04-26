namespace KF.Scripts.Models;

/// <summary>Represents a historical change to a script.</summary>
public sealed record ScriptHistoryRecord(
    long HistoryId,
    long ScriptId,
    string ApplicationId,
    string Name,
    string? OldContent,
    string? NewContent,
    bool? OldIsEnabled,
    bool? NewIsEnabled,
    byte[]? RowVersionBefore,
    byte[]? RowVersionAfter,
    string ChangedBy,
    DateTime ChangedDate,
    ScriptOperation Operation,
    string? Comment);
