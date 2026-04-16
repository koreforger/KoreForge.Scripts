# KoreForge.Scripts

A versioned script registry for the KoreForge ecosystem. Stores, versions, validates, and live-reloads scripts with full history tracking and rollback support.

## Packages

| Package | Description |
|---------|-------------|
| `KoreForge.Scripts` | Main bundler package — references all assemblies below |
| `KoreForge.Scripts.Abstractions` | Interfaces, models, options |
| `KoreForge.Scripts.Core` | Script store, history service, validation |
| `KoreForge.Scripts.Data` | SQL Server implementation |
| `KoreForge.Scripts.AspNet` | ASP.NET Core controllers and middleware |
| `KoreForge.Scripts.Cli` | CLI tool: upload, download, list, history, rollback |

## Quick Start

```csharp
// Register services
builder.Services.AddKoreForgeScripts(options =>
{
    options.ConnectionString = "...";
    options.Application = "MyApp";
    options.PollingInterval = TimeSpan.FromSeconds(30);
});

// Register a script compiler for validation
builder.Services.AddScriptCompiler<JexScriptCompiler>("jex");

// Map script management endpoints
app.MapScriptEndpoints();
```

## CLI

```bash
kf-scripts list --application MyApp
kf-scripts upload extract-login --file login.jex --application MyApp
kf-scripts download extract-login --output login.jex
kf-scripts history extract-login
kf-scripts rollback extract-login 0
```

## License

MIT — see [LICENSE.md](LICENSE.md)
