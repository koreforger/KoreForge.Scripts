using KF.Scripts.Data;
using KF.Scripts.Data.Entities;
using KF.Scripts.Exceptions;
using KF.Scripts.Interfaces;
using KF.Scripts.Models;
using KF.Scripts.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KF.Scripts.Core.Services;

public sealed class ScriptStore : IScriptStore
{
    private readonly IDbContextFactory<KFScriptsDbContext> _factory;
    private readonly ScriptStoreOptions _options;
    private readonly IEnumerable<IScriptCompiler> _compilers;
    private readonly ILogger<ScriptStore> _logger;

    public ScriptStore(
        IDbContextFactory<KFScriptsDbContext> factory,
        ScriptStoreOptions options,
        IEnumerable<IScriptCompiler> compilers,
        ILogger<ScriptStore> logger)
    {
        _factory = factory;
        _options = options;
        _compilers = compilers;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ScriptRecord>> ListAsync(string? typeTag = null, bool? isEnabled = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var q = db.Scripts.AsQueryable();

        if (_options.ApplicationId is not null)
            q = q.Where(s => s.ApplicationId == _options.ApplicationId);
        if (typeTag is not null)
            q = q.Where(s => s.TypeTag == typeTag);
        if (isEnabled.HasValue)
            q = q.Where(s => s.IsEnabled == isEnabled.Value);

        var entities = await q.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        return entities.Select(Map).ToList();
    }

    public async Task<ScriptRecord?> GetByIdAsync(long scriptId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.Scripts.AsNoTracking().FirstOrDefaultAsync(s => s.ScriptId == scriptId, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<ScriptRecord?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        IQueryable<ScriptEntity> q = db.Scripts.AsNoTracking();

        if (_options.ApplicationId is not null)
            q = q.Where(s => s.ApplicationId == _options.ApplicationId);

        var entity = await q.FirstOrDefaultAsync(s => s.Name == name, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<ScriptRecord> CreateAsync(CreateScriptRequest request, CancellationToken ct = default)
    {
        var appId = _options.ApplicationId ?? throw new InvalidOperationException("ApplicationId must be configured.");

        // Validate content length
        if (request.Content.Length > _options.MaxContentLength)
            throw new ArgumentException($"Script content exceeds maximum length of {_options.MaxContentLength}.");

        // Compile
        var compiler = ResolveCompiler(request.Language);
        if (compiler is not null)
        {
            var result = await compiler.CompileAsync(request.Content, ct);
            if (!result.Success)
                throw new ScriptCompilationException(result);
        }

        await using var db = await _factory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var entity = new ScriptEntity
        {
            ApplicationId = appId,
            Name = request.Name,
            TypeTag = request.TypeTag,
            Language = request.Language,
            Content = request.Content,
            Description = request.Description,
            IsEnabled = true,
            CreatedBy = request.CreatedBy,
            CreatedDate = now,
            ModifiedBy = request.CreatedBy,
            ModifiedDate = now,
            Comment = request.Comment
        };

        db.Scripts.Add(entity);
        await db.SaveChangesAsync(ct);

        // History
        db.ScriptHistory.Add(new ScriptHistoryEntity
        {
            ScriptId = entity.ScriptId,
            ApplicationId = entity.ApplicationId,
            Name = entity.Name,
            NewContent = entity.Content,
            NewIsEnabled = entity.IsEnabled,
            RowVersionAfter = entity.RowVersion,
            ChangedBy = request.CreatedBy,
            ChangedDate = now,
            Operation = nameof(ScriptOperation.Insert),
            Comment = request.Comment
        });
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Created script '{Name}' (Id={ScriptId})", entity.Name, entity.ScriptId);
        return Map(entity);
    }

    public async Task<ScriptRecord> UpdateAsync(UpdateScriptRequest request, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.Scripts.FirstOrDefaultAsync(s => s.ScriptId == request.ScriptId, ct)
            ?? throw new ScriptNotFoundException(request.ScriptId.ToString());

        if (!entity.RowVersion.SequenceEqual(request.RowVersion))
            throw new ScriptConcurrencyException(entity.ScriptId, entity.Name);

        var oldContent = entity.Content;
        var oldIsEnabled = entity.IsEnabled;
        var beforeRv = entity.RowVersion.ToArray();

        // Compile new content if changed
        if (request.Content is not null && request.Content != entity.Content)
        {
            if (request.Content.Length > _options.MaxContentLength)
                throw new ArgumentException($"Script content exceeds maximum length of {_options.MaxContentLength}.");

            var compiler = ResolveCompiler(entity.Language);
            if (compiler is not null)
            {
                var result = await compiler.CompileAsync(request.Content, ct);
                if (!result.Success)
                    throw new ScriptCompilationException(result);
            }
            entity.Content = request.Content;
        }

        if (request.Description is not null)
            entity.Description = request.Description;
        if (request.IsEnabled.HasValue)
            entity.IsEnabled = request.IsEnabled.Value;

        entity.ModifiedBy = request.ModifiedBy;
        entity.ModifiedDate = DateTime.UtcNow;
        entity.Comment = request.Comment;

        await db.SaveChangesAsync(ct);

        // History
        db.ScriptHistory.Add(new ScriptHistoryEntity
        {
            ScriptId = entity.ScriptId,
            ApplicationId = entity.ApplicationId,
            Name = entity.Name,
            OldContent = oldContent,
            NewContent = entity.Content,
            OldIsEnabled = oldIsEnabled,
            NewIsEnabled = entity.IsEnabled,
            RowVersionBefore = beforeRv,
            RowVersionAfter = entity.RowVersion,
            ChangedBy = request.ModifiedBy,
            ChangedDate = entity.ModifiedDate,
            Operation = nameof(ScriptOperation.Update),
            Comment = request.Comment
        });
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated script '{Name}' (Id={ScriptId})", entity.Name, entity.ScriptId);
        return Map(entity);
    }

    public async Task DeleteAsync(long scriptId, string changedBy, byte[] rowVersion, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.Scripts.FirstOrDefaultAsync(s => s.ScriptId == scriptId, ct)
            ?? throw new ScriptNotFoundException(scriptId.ToString());

        if (!entity.RowVersion.SequenceEqual(rowVersion))
            throw new ScriptConcurrencyException(entity.ScriptId, entity.Name);

        var oldContent = entity.Content;
        var oldIsEnabled = entity.IsEnabled;
        var beforeRv = entity.RowVersion.ToArray();
        var name = entity.Name;
        var appId = entity.ApplicationId;

        db.Scripts.Remove(entity);
        await db.SaveChangesAsync(ct);

        db.ScriptHistory.Add(new ScriptHistoryEntity
        {
            ScriptId = scriptId,
            ApplicationId = appId,
            Name = name,
            OldContent = oldContent,
            OldIsEnabled = oldIsEnabled,
            RowVersionBefore = beforeRv,
            ChangedBy = changedBy,
            ChangedDate = DateTime.UtcNow,
            Operation = nameof(ScriptOperation.Delete),
            Comment = null
        });
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted script '{Name}' (Id={ScriptId})", name, scriptId);
    }

    private IScriptCompiler? ResolveCompiler(string language)
        => _compilers.FirstOrDefault(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

    private static ScriptRecord Map(ScriptEntity e) => new(
        e.ScriptId, e.ApplicationId, e.Name, e.TypeTag, e.Language,
        e.Content, e.Description, e.IsEnabled,
        e.CreatedBy, e.CreatedDate, e.ModifiedBy, e.ModifiedDate,
        e.Comment, e.RowVersion);
}
