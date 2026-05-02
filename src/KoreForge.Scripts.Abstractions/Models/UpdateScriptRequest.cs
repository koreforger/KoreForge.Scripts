namespace KoreForge.Scripts.Models;

/// <summary>Request to update an existing script.</summary>
public sealed record UpdateScriptRequest(
    long ScriptId,
    string? Content,
    string? Description,
    bool? IsEnabled,
    byte[] RowVersion,
    string ModifiedBy,
    string? Comment);
