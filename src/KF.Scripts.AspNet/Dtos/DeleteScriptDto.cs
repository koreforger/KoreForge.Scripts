namespace KF.Scripts.AspNet.Dtos;

public sealed record DeleteScriptDto(
    string ChangedBy,
    string RowVersion);
