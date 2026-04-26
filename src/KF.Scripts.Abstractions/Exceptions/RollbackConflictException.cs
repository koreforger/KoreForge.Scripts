namespace KF.Scripts.Exceptions;

/// <summary>Thrown when a rollback target version conflicts with current state.</summary>
public sealed class RollbackConflictException : Exception
{
    public string ScriptName { get; }
    public int VersionIndex { get; }

    public RollbackConflictException(string scriptName, int versionIndex)
        : base($"Cannot rollback script '{scriptName}' to version index {versionIndex}.")
    {
        ScriptName = scriptName;
        VersionIndex = versionIndex;
    }
}
