namespace KF.Scripts.Exceptions;

/// <summary>Thrown when a script is not found.</summary>
public sealed class ScriptNotFoundException : Exception
{
    public ScriptNotFoundException(string identifier)
        : base($"Script not found: '{identifier}'.")
    {
    }
}
