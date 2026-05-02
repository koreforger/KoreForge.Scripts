namespace KoreForge.Scripts.AspNet.Dtos;

public sealed record UpdateScriptDto(
    string? Content,
    string? Description,
    bool? IsEnabled,
    string RowVersion,
    string ModifiedBy,
    string? Comment);
