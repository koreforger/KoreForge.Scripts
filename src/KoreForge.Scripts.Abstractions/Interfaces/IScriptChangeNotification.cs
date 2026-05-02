namespace KoreForge.Scripts.Interfaces;

/// <summary>Notifies subscribers when scripts have changed.</summary>
public interface IScriptChangeNotification
{
    event EventHandler<ScriptChangeEventArgs>? ScriptsChanged;
}

/// <summary>Event arguments for script change notifications.</summary>
public sealed class ScriptChangeEventArgs : EventArgs
{
    public required IReadOnlyList<long> ChangedScriptIds { get; init; }
    public required IReadOnlyList<long> DeletedScriptIds { get; init; }
    public required IReadOnlyList<long> AddedScriptIds { get; init; }
}
