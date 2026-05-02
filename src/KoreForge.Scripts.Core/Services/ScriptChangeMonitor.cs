using KoreForge.Scripts.Data;
using KoreForge.Scripts.Interfaces;
using KoreForge.Scripts.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KoreForge.Scripts.Core.Services;

public sealed class ScriptChangeMonitor : IScriptChangeNotification, IHostedService, IDisposable
{
    private readonly IDbContextFactory<KoreForgeScriptsDbContext> _factory;
    private readonly ScriptStoreOptions _options;
    private readonly ILogger<ScriptChangeMonitor> _logger;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    private Dictionary<long, byte[]> _knownVersions = new();

    public event EventHandler<ScriptChangeEventArgs>? ScriptsChanged;

    public ScriptChangeMonitor(
        IDbContextFactory<KoreForgeScriptsDbContext> factory,
        ScriptStoreOptions options,
        ILogger<ScriptChangeMonitor> logger)
    {
        _factory = factory;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollTask = PollLoopAsync(_cts.Token);
        _logger.LogInformation("Script change monitor started (polling every {Interval}s)", _options.PollingInterval.TotalSeconds);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_pollTask is not null)
                await Task.WhenAny(_pollTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
        _logger.LogInformation("Script change monitor stopped");
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        // Initial load
        await RefreshAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.PollingInterval, ct);
                await RefreshAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling for script changes");
            }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        IQueryable<Data.Entities.ScriptEntity> q = db.Scripts.AsNoTracking();

        if (_options.ApplicationId is not null)
            q = q.Where(s => s.ApplicationId == _options.ApplicationId);

        var current = await q
            .Select(s => new { s.ScriptId, s.RowVersion })
            .ToListAsync(ct);

        var currentDict = current.ToDictionary(s => s.ScriptId, s => s.RowVersion);
        var changed = new List<long>();
        var added = new List<long>();
        var deleted = new List<long>();

        // Detect added and changed
        foreach (var (id, rv) in currentDict)
        {
            if (!_knownVersions.TryGetValue(id, out var knownRv))
                added.Add(id);
            else if (!knownRv.SequenceEqual(rv))
                changed.Add(id);
        }

        // Detect deleted
        foreach (var id in _knownVersions.Keys)
        {
            if (!currentDict.ContainsKey(id))
                deleted.Add(id);
        }

        _knownVersions = currentDict;

        if (changed.Count > 0 || added.Count > 0 || deleted.Count > 0)
        {
            _logger.LogInformation("Script changes detected: {Added} added, {Changed} changed, {Deleted} deleted",
                added.Count, changed.Count, deleted.Count);

            ScriptsChanged?.Invoke(this, new ScriptChangeEventArgs
            {
                ChangedScriptIds = changed,
                DeletedScriptIds = deleted,
                AddedScriptIds = added
            });
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
