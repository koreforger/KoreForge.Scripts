namespace KoreForge.Scripts.Exceptions;

/// <summary>Thrown when a concurrency conflict is detected (RowVersion mismatch).</summary>
public sealed class ScriptConcurrencyException : Exception
{
    public long ScriptId { get; }
    public string ScriptName { get; }

    public ScriptConcurrencyException(long scriptId, string scriptName)
        : base($"Concurrency conflict on script '{scriptName}' (Id={scriptId}). The script was modified by another user.")
    {
        ScriptId = scriptId;
        ScriptName = scriptName;
    }
}
