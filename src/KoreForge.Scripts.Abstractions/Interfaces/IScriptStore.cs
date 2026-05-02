using KoreForge.Scripts.Models;

namespace KoreForge.Scripts.Interfaces;

/// <summary>Store for managing versioned scripts.</summary>
public interface IScriptStore
{
    Task<IReadOnlyList<ScriptRecord>> ListAsync(string? typeTag = null, bool? isEnabled = null, CancellationToken ct = default);
    Task<ScriptRecord?> GetByIdAsync(long scriptId, CancellationToken ct = default);
    Task<ScriptRecord?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<ScriptRecord> CreateAsync(CreateScriptRequest request, CancellationToken ct = default);
    Task<ScriptRecord> UpdateAsync(UpdateScriptRequest request, CancellationToken ct = default);
    Task DeleteAsync(long scriptId, string changedBy, byte[] rowVersion, CancellationToken ct = default);
}
