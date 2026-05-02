using KoreForge.Scripts.Models;

namespace KoreForge.Scripts.Interfaces;

/// <summary>Manages script change history and rollback.</summary>
public interface IScriptHistoryService
{
    Task<IReadOnlyList<ScriptHistoryRecord>> GetHistoryAsync(string name, CancellationToken ct = default);
    Task<ScriptRecord> RollbackAsync(string name, int versionIndex, string changedBy, CancellationToken ct = default);
}
