namespace KF.Scripts.AspNet.Dtos;

public sealed record RollbackDto(
    int VersionIndex,
    string ChangedBy);
