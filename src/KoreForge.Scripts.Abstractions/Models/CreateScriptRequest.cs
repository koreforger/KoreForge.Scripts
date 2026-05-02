namespace KoreForge.Scripts.Models;

/// <summary>Request to create a new script.</summary>
public sealed record CreateScriptRequest(
    string Name,
    string TypeTag,
    string Language,
    string Content,
    string? Description,
    string CreatedBy,
    string? Comment);
