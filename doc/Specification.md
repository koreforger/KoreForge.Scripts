# KoreForge.Scripts — Specification

## 1. Overview

KoreForge.Scripts is a versioned script registry for the KoreForge ecosystem. It provides storage, versioning, validation, live-reload, and rollback for scripts that applications use as dynamic, runtime-updatable logic.

KoreForge.Scripts does **not** know what scripts are used for. It stores them, versions them, validates them via pluggable compilers, and notifies consumers when they change. Applications (such as KafkaProcessor) decide how scripts map to their domain concepts (functions, rules, pipelines).

### Relationship to KoreForge.Settings

KoreForge.Settings manages **configuration values** — connection strings, thread counts, feature flags. Key-value pairs.

KoreForge.Scripts manages **executable logic** — scripts that are authored, tested, compiled, and deployed as living code. They require:

- **Pre-save validation** — compile the script before persisting; reject syntax errors before they reach production
- **Richer metadata** — script type tags, language identifiers, descriptions
- **Different access patterns** — scripts are fetched by ID/name and compiled into in-memory programs, not read as configuration keys
- **Different lifecycle** — scripts go through author → test → upload → shadow-test → promote → monitor, not just set/get

Both libraries share the same infrastructure patterns: SQL-backed storage, RowVersion concurrency, full history with audit trail, polling-based live reload, CLI tooling.

## 2. Assemblies

| Assembly | NuGet Package | Purpose |
|----------|--------------|---------|
| `KF.Scripts.Abstractions` | (bundled) | Interfaces, models, options, exceptions |
| `KF.Scripts.Core` | (bundled) | `ScriptStore`, `ScriptHistoryService`, `ScriptChangeMonitor` |
| `KF.Scripts.Data` | (bundled) | SQL Server implementation, migrations |
| `KF.Scripts.AspNet` | (bundled) | ASP.NET Core endpoint mappings |
| `KF.Scripts` | `KoreForge.Scripts` | Main bundler package (ships all above DLLs) |
| `KF.Scripts.Cli` | `KoreForge.Scripts.Cli` | Dotnet tool: `kf-scripts` |

All assemblies target `net10.0`.

## 3. Database Schema

### 3.1 Scripts Table

```sql
CREATE TABLE dbo.Scripts (
    ScriptId        BIGINT          IDENTITY(1,1) PRIMARY KEY,
    ApplicationId   NVARCHAR(200)   NOT NULL,
    Name            NVARCHAR(500)   NOT NULL,
    TypeTag         NVARCHAR(100)   NOT NULL,      -- "extract", "rule", "transform", etc.
    Language        NVARCHAR(50)    NOT NULL DEFAULT 'jex',
    Content         NVARCHAR(MAX)   NOT NULL,
    Description     NVARCHAR(2000)  NULL,
    IsEnabled       BIT             NOT NULL DEFAULT(1),
    CreatedBy       NVARCHAR(50)    NOT NULL,
    CreatedDate     DATETIME2(3)    NOT NULL DEFAULT(SYSUTCDATETIME()),
    ModifiedBy      NVARCHAR(50)    NOT NULL,
    ModifiedDate    DATETIME2(3)    NOT NULL DEFAULT(SYSUTCDATETIME()),
    Comment         NVARCHAR(4000)  NULL,
    RowVersion      ROWVERSION      NOT NULL,

    CONSTRAINT UX_Scripts_App_Name UNIQUE (ApplicationId, Name)
);

CREATE INDEX IX_Scripts_TypeTag ON dbo.Scripts(ApplicationId, TypeTag, IsEnabled);
```

**Design decisions:**

- **`ApplicationId`** — Multi-application scoping (same pattern as Settings). KafkaProcessor scripts don't collide with EventProcessor scripts.
- **`Name`** — Human-readable unique identifier within an application. Examples: `extract-login`, `rule-velocity-check`, `transform-normalize-amount`.
- **`TypeTag`** — Application-defined discriminator. The library doesn't interpret this. KafkaProcessor uses `"extract"` and `"rule"`. Another app might use `"transform"`, `"validation"`, `"enrichment"`.
- **`Language`** — Defaults to `"jex"`. Extensible for future script engines.
- **`Content`** — The full script source text. NVARCHAR(MAX) supports scripts of any size.
- **`RowVersion`** — SQL Server timestamp for optimistic concurrency. Prevents lost updates.

### 3.2 ScriptHistory Table

```sql
CREATE TABLE dbo.ScriptHistory (
    HistoryId           BIGINT          IDENTITY(1,1) PRIMARY KEY,
    ScriptId            BIGINT          NULL,
    ApplicationId       NVARCHAR(200)   NOT NULL,
    Name                NVARCHAR(500)   NOT NULL,
    OldContent          NVARCHAR(MAX)   NULL,
    NewContent          NVARCHAR(MAX)   NULL,
    OldIsEnabled        BIT             NULL,
    NewIsEnabled        BIT             NULL,
    RowVersionBefore    VARBINARY(8)    NULL,
    RowVersionAfter     VARBINARY(8)    NULL,
    ChangedBy           NVARCHAR(50)    NOT NULL,
    ChangedDate         DATETIME2(3)    NOT NULL DEFAULT(SYSUTCDATETIME()),
    Operation           NVARCHAR(20)    NOT NULL,     -- Insert, Update, Delete, Rollback
    Comment             NVARCHAR(4000)  NULL,

    CONSTRAINT FK_ScriptHistory_Scripts
        FOREIGN KEY (ScriptId) REFERENCES dbo.Scripts(ScriptId)
);

CREATE INDEX IX_ScriptHistory_ScriptId ON dbo.ScriptHistory(ScriptId, ChangedDate DESC);
CREATE INDEX IX_ScriptHistory_App_Name ON dbo.ScriptHistory(ApplicationId, Name, ChangedDate DESC);
```

**Design decisions:**

- Every mutation (insert, update, delete, rollback) produces a history record.
- `OldContent` and `NewContent` capture the full script before and after. This enables content diffing in the UI and safe rollback.
- `RowVersionBefore`/`RowVersionAfter` enable rollback conflict detection (same pattern as Settings).
- `Comment` — optional note explaining why the change was made (audit trail).
- `ScriptId` FK is nullable to retain history after delete.

## 4. Abstractions

### 4.1 Models

```csharp
namespace KF.Scripts.Abstractions.Models;

public sealed record ScriptRecord
{
    public long ScriptId { get; init; }
    public required string ApplicationId { get; init; }
    public required string Name { get; init; }
    public required string TypeTag { get; init; }
    public string Language { get; init; } = "jex";
    public required string Content { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
    public required string CreatedBy { get; init; }
    public DateTime CreatedDate { get; init; }
    public required string ModifiedBy { get; init; }
    public DateTime ModifiedDate { get; init; }
    public string? Comment { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed record ScriptHistoryRecord
{
    public long HistoryId { get; init; }
    public long? ScriptId { get; init; }
    public required string ApplicationId { get; init; }
    public required string Name { get; init; }
    public string? OldContent { get; init; }
    public string? NewContent { get; init; }
    public bool? OldIsEnabled { get; init; }
    public bool? NewIsEnabled { get; init; }
    public byte[]? RowVersionBefore { get; init; }
    public byte[]? RowVersionAfter { get; init; }
    public required string ChangedBy { get; init; }
    public DateTime ChangedDate { get; init; }
    public required ScriptOperation Operation { get; init; }
    public string? Comment { get; init; }
}

public enum ScriptOperation
{
    Insert,
    Update,
    Delete,
    Rollback
}
```

### 4.2 Options

```csharp
namespace KF.Scripts.Abstractions;

public sealed class ScriptStoreOptions
{
    /// <summary>SQL Server connection string.</summary>
    public required string ConnectionString { get; set; }

    /// <summary>Application identifier for multi-app scoping.</summary>
    public required string Application { get; set; }

    /// <summary>Polling interval for change detection. Zero disables polling.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum script content length (bytes). Default 1 MB.</summary>
    public int MaxContentLength { get; set; } = 1_048_576;
}
```

### 4.3 Interfaces

```csharp
namespace KF.Scripts.Abstractions.Interfaces;

/// <summary>
/// CRUD operations for scripts with mandatory pre-save validation.
/// </summary>
public interface IScriptStore
{
    Task<IReadOnlyList<ScriptRecord>> ListAsync(
        string? typeTag = null,
        bool? isEnabled = null,
        CancellationToken ct = default);

    Task<ScriptRecord?> GetByIdAsync(long scriptId, CancellationToken ct = default);

    Task<ScriptRecord?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Create a new script. Content is validated via the registered IScriptCompiler
    /// before persisting. Throws <see cref="ScriptCompilationException"/> on failure.
    /// </summary>
    Task<ScriptRecord> CreateAsync(
        CreateScriptRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Update script content. Validates new content. Requires matching RowVersion
    /// for optimistic concurrency. Archives old version to history.
    /// </summary>
    Task<ScriptRecord> UpdateAsync(
        UpdateScriptRequest request,
        CancellationToken ct = default);

    Task DeleteAsync(long scriptId, byte[] rowVersion, string deletedBy, CancellationToken ct = default);
}

/// <summary>
/// History retrieval and rollback.
/// </summary>
public interface IScriptHistoryService
{
    Task<IReadOnlyList<ScriptHistoryRecord>> GetHistoryAsync(
        long scriptId,
        CancellationToken ct = default);

    /// <summary>
    /// Rollback a script to a previous version. The version at the given index
    /// (0 = most recent history entry) is restored. The rollback itself is recorded
    /// as a new history entry. RowVersion conflict detection prevents concurrent modifications.
    /// </summary>
    Task<ScriptRecord> RollbackAsync(
        string name,
        int versionIndex,
        string rolledBackBy,
        CancellationToken ct = default);
}

/// <summary>
/// Pluggable script compiler. Applications register implementations for each
/// language they support. The library calls this before any Create or Update
/// to validate script syntax.
/// </summary>
public interface IScriptCompiler
{
    /// <summary>The script language this compiler handles (e.g., "jex").</summary>
    string Language { get; }

    /// <summary>
    /// Attempt to compile the script content. Returns a result indicating success
    /// or failure with diagnostic messages.
    /// </summary>
    Task<CompilationResult> CompileAsync(string content, CancellationToken ct = default);
}

/// <summary>
/// Validate-only endpoint — compile a script without saving it.
/// Used by the UI for real-time error checking before upload.
/// </summary>
public interface IScriptValidator
{
    Task<CompilationResult> ValidateAsync(
        string content,
        string language = "jex",
        CancellationToken ct = default);
}
```

### 4.4 Request/Result Models

```csharp
public sealed record CreateScriptRequest
{
    public required string Name { get; init; }
    public required string TypeTag { get; init; }
    public string Language { get; init; } = "jex";
    public required string Content { get; init; }
    public string? Description { get; init; }
    public required string CreatedBy { get; init; }
    public string? Comment { get; init; }
}

public sealed record UpdateScriptRequest
{
    public required long ScriptId { get; init; }
    public required string Content { get; init; }
    public string? Description { get; init; }
    public required byte[] RowVersion { get; init; }
    public required string ModifiedBy { get; init; }
    public string? Comment { get; init; }
}

public sealed record CompilationResult
{
    public bool Success { get; init; }
    public IReadOnlyList<CompilationDiagnostic> Diagnostics { get; init; } = [];
}

public sealed record CompilationDiagnostic
{
    public required DiagnosticSeverity Severity { get; init; }
    public required string Message { get; init; }
    public int? Line { get; init; }
    public int? Column { get; init; }
}

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}
```

### 4.5 Exceptions

```csharp
public class ScriptCompilationException : Exception
{
    public CompilationResult Result { get; }
}

public class ScriptConcurrencyException : Exception
{
    public long ScriptId { get; }
    public string Name { get; }
}

public class RollbackConflictException : Exception
{
    public string Name { get; }
    public int VersionIndex { get; }
}
```

## 5. Core Services

### 5.1 ScriptStore

The primary service implementing `IScriptStore`.

**Create flow:**
1. Validate input (name, content length, required fields)
2. Resolve `IScriptCompiler` for the given language
3. Call `CompileAsync(content)` — throw `ScriptCompilationException` if failed
4. INSERT into `dbo.Scripts`
5. INSERT history record (Operation = Insert)
6. Return the created `ScriptRecord`

**Update flow:**
1. SELECT current script with `UPDLOCK` (optimistic concurrency via RowVersion check)
2. If RowVersion mismatch → throw `ScriptConcurrencyException`
3. Resolve `IScriptCompiler` and compile new content — reject on failure
4. UPDATE `dbo.Scripts` (Content, ModifiedBy, ModifiedDate, Comment)
5. INSERT history record (OldContent = previous, NewContent = new, Operation = Update)
6. Return updated `ScriptRecord`

**Delete flow:**
1. SELECT with UPDLOCK + RowVersion check
2. DELETE from `dbo.Scripts`
3. INSERT history record (Operation = Delete, OldContent = deleted content)

### 5.2 ScriptHistoryService

Implements `IScriptHistoryService`.

**GetHistory flow:**
1. SELECT from `dbo.ScriptHistory` WHERE ScriptId = @id ORDER BY ChangedDate DESC
2. Return as `IReadOnlyList<ScriptHistoryRecord>`

**Rollback flow:**
1. Fetch history for the script, ordered by date DESC
2. Validate `versionIndex` is in range (0 = most recent history entry)
3. Get the target history record — the content to restore is `OldContent`
4. Fetch current script, check RowVersion matches `RowVersionAfter` of the latest history entry
5. If mismatch → throw `RollbackConflictException` (someone changed the script after the version we're rolling back)
6. Compile the restored content (safety check — the old version should still compile, but verify)
7. UPDATE script with restored content
8. INSERT history record (Operation = Rollback, OldContent = current, NewContent = restored)

### 5.3 ScriptChangeMonitor

A background service (`IHostedService`) that polls the database for changes and notifies consumers.

**Pattern:** Identical to `SqlSettingsConfigurationProvider` polling loop.

```
1. Load all scripts for ApplicationId → build dictionary { Name → (ScriptId, RowVersion) }
2. Sleep PollingInterval
3. Re-query → compare RowVersion values
4. If any changed → fire IScriptChangeNotification with list of changed script IDs
5. Repeat
```

**Consumer integration:**
```csharp
public interface IScriptChangeNotification
{
    /// <summary>
    /// Fired when the polling loop detects script changes in the database.
    /// </summary>
    event Action<ScriptChangeEvent> ScriptsChanged;
}

public sealed record ScriptChangeEvent
{
    public DateTimeOffset DetectedAt { get; init; }
    public IReadOnlyList<long> ChangedScriptIds { get; init; } = [];
    public IReadOnlyList<long> DeletedScriptIds { get; init; } = [];
}
```

Applications subscribe to `ScriptsChanged` to trigger recompilation and hot-swap.

### 5.4 ScriptValidator

Implements `IScriptValidator`. Resolves the correct `IScriptCompiler` by language and delegates to it. This is the "compile check without saving" path used by the UI.

## 6. ASP.NET Core Integration (KF.Scripts.AspNet)

### 6.1 Endpoint Mappings

```csharp
public static class ScriptEndpoints
{
    public static IEndpointRouteBuilder MapScriptEndpoints(
        this IEndpointRouteBuilder app,
        string prefix = "/api/scripts")
    {
        var group = app.MapGroup(prefix);

        group.MapGet("/", ListScripts);
        group.MapGet("/{id:long}", GetScript);
        group.MapGet("/by-name/{name}", GetScriptByName);
        group.MapPost("/", CreateScript);
        group.MapPut("/{id:long}", UpdateScript);
        group.MapDelete("/{id:long}", DeleteScript);
        group.MapGet("/{id:long}/history", GetHistory);
        group.MapPost("/by-name/{name}/rollback", RollbackScript);
        group.MapPost("/validate", ValidateScript);
        group.MapPost("/{id:long}/test", TestScript);

        return app;
    }
}
```

### 6.2 Test Endpoint

`POST /api/scripts/{id}/test` accepts a script ID (or inline content) plus a JSON input payload. It compiles the script, runs it against the input, and returns the output. This is the backend for the `<KfScriptTester>` component.

**Request:**
```json
{
    "input": { "Action": "Login", "SessionId": "abc-123", ... },
    "scriptId": 42,
    "content": null
}
```

One of `scriptId` (run persisted script) or `content` (run ad-hoc script) must be provided.

**Response:**
```json
{
    "success": true,
    "output": { "SessionID": "abc-123", "Amount": 150.00 },
    "diagnostics": [],
    "executionTimeMs": 0.42
}
```

### 6.3 DI Registration

```csharp
public static IServiceCollection AddKoreForgeScripts(
    this IServiceCollection services,
    Action<ScriptStoreOptions> configure)
{
    services.Configure(configure);
    services.AddSingleton<IScriptStore, ScriptStore>();
    services.AddSingleton<IScriptHistoryService, ScriptHistoryService>();
    services.AddSingleton<IScriptValidator, ScriptValidator>();
    services.AddSingleton<IScriptChangeNotification, ScriptChangeMonitor>();
    services.AddHostedService(sp =>
        (ScriptChangeMonitor)sp.GetRequiredService<IScriptChangeNotification>());
    return services;
}

public static IServiceCollection AddScriptCompiler<TCompiler>(
    this IServiceCollection services,
    string language)
    where TCompiler : class, IScriptCompiler
{
    services.AddKeyedSingleton<IScriptCompiler, TCompiler>(language);
    return services;
}
```

## 7. CLI Tool (KF.Scripts.Cli)

Packaged as a .NET tool: `dotnet tool install -g KoreForge.Scripts.Cli`

### 7.1 Commands

| Command | Usage | Description |
|---------|-------|-------------|
| `list` | `kf-scripts list [--type extract] [--enabled]` | List all scripts, optionally filtered |
| `get` | `kf-scripts get <id>` | Display script metadata as JSON |
| `download` | `kf-scripts download <name> [--output file.jex]` | Download script content to file or stdout |
| `upload` | `kf-scripts upload <name> --file script.jex --type extract [--comment "..."]` | Upload/update script with validation |
| `create` | `kf-scripts create <name> --file script.jex --type extract [--description "..."]` | Create new script |
| `delete` | `kf-scripts delete <id> --rowversion <hex>` | Delete script with concurrency check |
| `history` | `kf-scripts history <name>` | Show version history |
| `rollback` | `kf-scripts rollback <name> <versionIndex>` | Rollback to previous version |
| `validate` | `kf-scripts validate --file script.jex [--language jex]` | Compile-check without saving |
| `export` | `kf-scripts export --output scripts.json [--type extract]` | Export scripts to JSON |
| `import` | `kf-scripts import --file scripts.json [--apply] [--upsert]` | Import scripts from JSON |

### 7.2 Global Options

| Option | Description |
|--------|-------------|
| `--connection` | SQL Server connection string (or env `KF_SCRIPTS_CONNECTION`) |
| `--application` | Application ID (or env `KF_SCRIPTS_APPLICATION`) |
| `--format` | Output format: `table` (default), `json`, `csv` |

### 7.3 Upload Workflow

```
$ kf-scripts upload extract-login --file login-extract.jex --type extract --comment "Added fallback for sessionId casing"

Validating login-extract.jex... ✓ compiled successfully
Uploading to 'extract-login' (KafkaProcessor)...
  Previous version archived (HistoryId: 847)
  New version saved (ScriptId: 42, RowVersion: 0x00000000002A)
Done.
```

## 8. Testing Strategy

### 8.1 Unit Tests (KF.Scripts.Tests)

- **ScriptStore tests** — CRUD operations, validation enforcement, concurrency conflicts
- **ScriptHistoryService tests** — history retrieval, rollback mechanics, conflict detection
- **ScriptChangeMonitor tests** — polling detection, change event firing
- **ScriptValidator tests** — compiler resolution, compilation delegation
- **Endpoint tests** — request/response mapping, error handling

### 8.2 Test infrastructure

- In-memory SQL (or SQLite) for data layer tests
- Mock `IScriptCompiler` for testing validation flow without real JEX dependency
- Follows existing patterns: xUnit, FluentAssertions, Moq, coverlet

### 8.3 Coverage Target

70%+ line coverage, enforced by coverlet.runsettings.

## 9. Build & Packaging

### 9.1 NuGet Packages

| Package | Description |
|---------|-------------|
| `KoreForge.Scripts` | Bundled package containing all 5 assemblies |
| `KoreForge.Scripts.Cli` | Dotnet tool package |

### 9.2 Versioning

MinVer with tag prefix `KoreForge.Scripts/v`. Example: `KoreForge.Scripts/v0.0.1-alpha`.

### 9.3 Build Scripts (bin/)

Following existing KoreForge convention:

| Script | Purpose |
|--------|---------|
| `build-test.ps1` | Build + run tests |
| `build-test-codecoverage.ps1` | Build + test + HTML coverage report |
| `build-rebuild.ps1` | Clean rebuild |
| `git-push.ps1` | Push to remote |
| `git-push-nuget.ps1` | Tag + push NuGet version |

## 10. Non-Goals (v1)

- **Encryption** — Scripts are code, not secrets. No encryption support needed.
- **Binary scripts** — Content is always text (NVARCHAR). Binary script formats are out of scope.
- **Script dependencies** — JEX `%func` shared libraries are a future concern. v1 treats each script as standalone.
- **Multi-language compilation in one request** — Each script has one language. Cross-language composition is out of scope.
- **Authentication/authorization** — The library provides endpoints; the host application applies auth policies.
