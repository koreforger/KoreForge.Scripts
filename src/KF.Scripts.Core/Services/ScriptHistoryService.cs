using KF.Scripts.Data;
using KF.Scripts.Data.Entities;
using KF.Scripts.Exceptions;
using KF.Scripts.Interfaces;
using KF.Scripts.Models;
using KF.Scripts.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KF.Scripts.Core.Services;

public sealed class ScriptHistoryService : IScriptHistoryService
{
    private readonly IDbContextFactory<KFScriptsDbContext> _factory;
    private readonly IScriptStore _scriptStore;
    private readonly ScriptStoreOptions _options;
    private readonly ILogger<ScriptHistoryService> _logger;

    public ScriptHistoryService(
        IDbContextFactory<KFScriptsDbContext> factory,
        IScriptStore scriptStore,
        ScriptStoreOptions options,
        ILogger<ScriptHistoryService> logger)
    {
        _factory = factory;
        _scriptStore = scriptStore;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ScriptHistoryRecord>> GetHistoryAsync(string name, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        IQueryable<ScriptHistoryEntity> q = db.ScriptHistory.AsNoTracking();

        if (_options.ApplicationId is not null)
            q = q.Where(h => h.ApplicationId == _options.ApplicationId);

        var entities = await q
            .Where(h => h.Name == name)
            .OrderByDescending(h => h.ChangedDate)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    public async Task<ScriptRecord> RollbackAsync(string name, int versionIndex, string changedBy, CancellationToken ct = default)
    {
        var history = await GetHistoryAsync(name, ct);

        if (versionIndex < 0 || versionIndex >= history.Count)
            throw new RollbackConflictException(name, versionIndex);

        var target = history[versionIndex];
        var restoredContent = target.Operation == ScriptOperation.Delete
            ? target.OldContent
            : target.OldContent ?? target.NewContent;

        if (restoredContent is null)
            throw new RollbackConflictException(name, versionIndex);

        // Find current script
        var current = await _scriptStore.GetByNameAsync(name, ct)
            ?? throw new ScriptNotFoundException(name);

        // Update with rollback
        var result = await _scriptStore.UpdateAsync(new UpdateScriptRequest(
            current.ScriptId,
            restoredContent,
            null,
            null,
            current.RowVersion,
            changedBy,
            $"Rollback to version index {versionIndex}"), ct);

        // Re-tag the last history entry as Rollback
        await using var db = await _factory.CreateDbContextAsync(ct);
        var lastEntry = await db.ScriptHistory
            .Where(h => h.Name == name)
            .OrderByDescending(h => h.ChangedDate)
            .FirstOrDefaultAsync(ct);

        if (lastEntry is not null)
        {
            lastEntry.Operation = nameof(ScriptOperation.Rollback);
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Rolled back script '{Name}' to version index {Index}", name, versionIndex);
        return result;
    }

    private static ScriptHistoryRecord Map(ScriptHistoryEntity e) => new(
        e.HistoryId, e.ScriptId, e.ApplicationId, e.Name,
        e.OldContent, e.NewContent, e.OldIsEnabled, e.NewIsEnabled,
        e.RowVersionBefore, e.RowVersionAfter,
        e.ChangedBy, e.ChangedDate,
        Enum.TryParse<ScriptOperation>(e.Operation, out var op) ? op : ScriptOperation.Update,
        e.Comment);
}
