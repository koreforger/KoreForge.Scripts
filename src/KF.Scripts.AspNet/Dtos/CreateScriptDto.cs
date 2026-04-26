namespace KF.Scripts.AspNet.Dtos;

public sealed record CreateScriptDto(
    string Name,
    string TypeTag,
    string Language,
    string Content,
    string? Description,
    string CreatedBy,
    string? Comment);
