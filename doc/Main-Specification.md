# KoreForge Scripts Feature — Main Specification

## 1. Problem Statement

KafkaProcessor processes Kafka messages through a pipeline that classifies messages by action type, matches them to function definitions, and extracts fields using JEX scripts. Currently, JEX scripts are **auto-generated in memory** from `FieldExtractionRules` database rows and compiled at startup. This means:

- Scripts cannot be edited without changing extraction rule rows and restarting the application
- There is no way to test a script change before it goes live
- There is no version history or rollback capability for script changes
- The fine-grained control that JEX provides (coalesce paths, conditional logic, type conversions) is limited to what the auto-generator can produce

JEX was designed to be human-authored and editable at runtime. The current architecture defeats this purpose.

### What We Want

1. **Scripts are first-class artifacts** — authored in VS Code with full language server support, stored in a database with versioning, deployed without application restarts
2. **Safe deployment** — compile-check before saving, shadow testing against live traffic before promoting, one-click rollback if something goes wrong
3. **Operational simplicity** — no corporate change control process for script updates; author, test, upload, done
4. **Future-proof** — design accommodates rule scripts (many-to-many with functions) even though v1 only implements extraction scripts

## 2. Solution Architecture

Four projects work together to deliver this capability:

```
┌─────────────────────────────────────────────────────────────────┐
│                        KafkaProcessor                           │
│                     (ASP.NET Core App)                          │
│                                                                 │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────────────┐  │
│  │ Controllers  │  │  SignalR Hubs │  │      Services         │  │
│  │ /api/funcs   │  │ /hub/metrics │  │ ScriptReloadService   │  │
│  │ /api/shadow  │  │ /hub/shadow  │  │ ShadowTestService     │  │
│  │              │  │ /hub/settings│  │ FunctionScriptRepo    │  │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬────────────┘  │
│         │                 │                      │               │
│  ┌──────┴─────────────────┴──────────────────────┴────────────┐  │
│  │                    KoreForge.Scripts                        │  │
│  │  IScriptStore  │  IScriptHistoryService  │  Polling/Reload │  │
│  │  IScriptCompiler (→ JEX)  │  /api/scripts endpoints       │  │
│  └────────────────────────────────────────────────────────────┘  │
│         │                                                        │
│  ┌──────┴─────────────────────────────────────────────────────┐  │
│  │                      SQL Server                             │  │
│  │  Scripts │ ScriptHistory │ FunctionScripts │ FunctionDefs   │  │
│  └─────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────┬──────────────────────────┘
                                       │
                                       │ HTTP + SignalR
                                       │
┌──────────────────────────────────────┴──────────────────────────┐
│                   KafkaProcessor Dashboard                      │
│                      (Vue 3 SPA)                                │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                  @koreforge/vue-scripts                      ││
│  │  KfScriptEditor │ KfHistoryViewer │ KfScriptTester          ││
│  │  KfRollbackDialog │ KfCompileStatus                         ││
│  │  useScriptEditor │ useScriptHistory │ useSignalRStream      ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  Application-Specific Views:                                    │
│  FunctionsView │ ShadowTestView │ SettingsView │ MetricsView   │
└─────────────────────────────────────────────────────────────────┘
```

## 3. Project Inventory

| # | Project | Type | Repo | Location | Description |
|---|---------|------|------|----------|-------------|
| 1 | **KoreForge.Scripts** | lib (nuget/koreforge) | `koreforger/KoreForge.Scripts` | `KoreForge.Scripts/` | Versioned script registry — storage, history, rollback, validation, polling, ASP.NET endpoints, CLI |
| 2 | **KoreForge.Vue.Scripts** | weblib (npm/koreforge) | `koreforger/KoreForge.Vue.Scripts` | `KoreForge.Vue.Scripts/` | Themeable Vue 3 component library — script editor, history viewer, tester, rollback dialog |
| 3 | **KafkaProcessor.Dashboard** | web (kafkaprocessor) | `koreforger/KafkaProcessor.Dashboard` | `KafkaProcessor.Dashboard/` | Application-specific SPA — functions, scripts, shadow testing, settings, metrics |
| 4 | **KafkaProcessor.Scripts.API** | app (kafkaprocessor) | `koreforger/KafkaProcessor.Scripts.API` | `KafkaProcessor.Scripts.API/` | Backend library — controllers, hubs, shadow test service, script reload, function-script assignments |

### Boundary Rules

- **KoreForge.Scripts** knows about scripts. It does NOT know about functions, rules, Kafka, or any application domain.
- **KoreForge.Vue.Scripts** knows about script editing, history, and testing. It does NOT know about functions, rules, or shadow testing.
- **KafkaProcessor.Scripts.API** knows about functions, rules, shadow testing. It uses KoreForge.Scripts for storage.
- **KafkaProcessor.Dashboard** uses KoreForge.Vue.Scripts for UI components and calls KafkaProcessor.Scripts.API endpoints.

The test: if it says "script", it's KoreForge. If it says "function", "rule", or "shadow", it's KafkaProcessor.

## 4. Data Flow

### 4.1 Script Authoring & Deployment

```
Developer's Machine                    Server
─────────────────                      ──────

1. Author .jex file in VS Code
   (language server provides
   completions, diagnostics,
   hover docs)

2. Test locally:
   jex extract-login.jex
   --input sample.json
   → see output

3. Upload:                          4. KoreForge.Scripts receives:
   kf-scripts upload                   - Validates name/type/language
     extract-login                     - Compiles via IScriptCompiler
     --file extract-login.jex          - If fails → 400 + diagnostics
     --type extract                    - If passes → INSERT Scripts
     --comment "Added amount"          - INSERT ScriptHistory (Operation=Insert)
                                       - Return ScriptRecord

                                    5. Polling loop detects change:
                                       - ScriptChangeMonitor fires event
                                       - ScriptReloadService receives event
                                       - Recompiles changed script
                                       - Atomic swap of FrozenDictionary
                                       - Log: "Script extract-login reloaded"

                                    6. Next message processed uses new script
                                       (within PollingInterval, default 30s)
```

### 4.2 Shadow Testing

```
Dashboard (Browser)                    KafkaProcessor (Server)
───────────────────                    ───────────────────────

1. User selects function "Login"
   Pastes candidate JEX script
   Sets sample size = 20
   Clicks "Start Shadow Test"

2. POST /api/shadow-test          →   3. Compile candidate script
   { functionId: 1,                      If fails → 400 + diagnostics
     candidateContent: "...",            Register ShadowSession
     sampleSize: 20 }                   Return { sessionId }

4. Connect to SignalR                  
   /hub/shadow-test
   JoinSession(sessionId)

                                       5. Pipeline processes message:
                                          - Action matches "Login"
                                          - Run CURRENT extract script → output A
                                          - Run CANDIDATE extract script → output B
                                          - Compute field diff (A vs B)
                                          - Send "shadow-result" to session group

6. Receives shadow-result          ←   
   Table row appears:
   | SessionID | x9f2... | x9f2... | Match |
   | Amount    | 150.00  | 150     | Changed |

                                       7. After 20 matches (or 5min timeout):
                                          Send "shadow-complete" summary

8. User sees summary:             ←
   17/20 match, 3 diffs
   
   [Promote] or [Discard]

9. User clicks [Promote]          →   10. Save candidate to Scripts table
   POST /shadow-test/{id}/promote        (with history record)
                                         Trigger reload
                                         Return updated ScriptRecord

11. Script goes live.
    Old version in history.
    Rollback available.
```

### 4.3 Rollback

```
Dashboard (Browser)                    KafkaProcessor (Server)
───────────────────                    ───────────────────────

1. User opens script "extract-login"
   Clicks "History" tab
   Sees version timeline

2. Clicks version from 2 hours ago
   Side-by-side diff shows changes

3. Clicks "Rollback to this version"
   Confirmation dialog appears
   User confirms

4. POST /scripts/by-name/         →   5. Validate versionIndex
   extract-login/rollback                Check RowVersion (no conflict)
   { versionIndex: 0 }                  Compile old content (safety check)
                                         UPDATE Scripts with old content
                                         INSERT ScriptHistory (Operation=Rollback)
                                         Return updated ScriptRecord

                                       6. Polling loop detects change
                                          Recompiles and hot-swaps
                                          Log: "Script extract-login rolled back"

7. Editor refreshes with          ←
   restored content
```

## 5. Database Schema (Complete)

### Tables Owned by KoreForge.Scripts

| Table | Owner | Purpose |
|-------|-------|---------|
| `dbo.Scripts` | KoreForge.Scripts | Script content, metadata, versioning |
| `dbo.ScriptHistory` | KoreForge.Scripts | Full change history with audit trail |

### Tables Owned by KafkaProcessor

| Table | Owner | Purpose |
|-------|-------|---------|
| `dbo.FunctionDefinitions` | KafkaProcessor | Function ID, name, action regex, group |
| `dbo.FunctionScripts` | KafkaProcessor | Junction: which scripts are assigned to which functions |
| `dbo.FieldExtractionRules` | KafkaProcessor | Legacy — retained for migration reference |
| `dbo.Settings` | KoreForge.Settings | Application configuration |
| `dbo.SettingsHistory` | KoreForge.Settings | Settings change history |

### Entity Relationships

```
FunctionDefinitions (1) ──── (*) FunctionScripts (*) ──── (1) Scripts
                                                              │
                                                              │ (1)
                                                              │
                                                         (*) ScriptHistory
```

## 6. Dependency Graph

```
KafkaProcessor Dashboard (Vue 3 SPA)
  ├── @koreforge/vue-scripts (npm)
  │   ├── vue ^3.5.0
  │   ├── @microsoft/signalr ^8.0.0
  │   └── monaco-editor ^0.48.0
  ├── vue ^3.5.0
  ├── vue-router ^4.5.0
  └── @microsoft/signalr ^8.0.0

KafkaProcessor (ASP.NET Core)
  ├── KoreForge.Scripts (nuget)
  │   └── Microsoft.Data.SqlClient
  ├── KoreForge.Jex (nuget)           ← provides IScriptCompiler implementation
  ├── KoreForge.Kafka (nuget)
  ├── KoreForge.Processing (nuget)
  ├── KoreForge.Settings (nuget)
  ├── KoreForge.Logging (nuget)
  ├── KoreForge.Metrics (nuget)
  └── KoreForge.Web.HealthChecks (nuget)
```

### Dependency Rules

- KoreForge.Scripts does **not** depend on KoreForge.Jex. The compilation bridge is in the application.
- KF.Vue.Scripts does **not** depend on the KafkaProcessor backend. It works with any KoreForge.Scripts backend.
- The Dashboard does **not** depend on DevExtreme or any paid UI library.

## 7. Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Backend Runtime | .NET 10.0 | 10.0 |
| Backend Framework | ASP.NET Core | 10.0 |
| Database | SQL Server / Azure SQL Edge | Latest |
| Real-time | SignalR | 10.0 |
| Frontend Framework | Vue 3 | ^3.5.0 |
| Frontend Build | Vite | ^6.2.0 |
| Code Editor | Monaco Editor | ^0.48.0 |
| TypeScript | TypeScript | ~5.7 |
| Script Language | KoreForge.Jex | 0.0.2-alpha |
| Package Manager (NuGet) | Central Package Management | Via Directory.Packages.props |
| Package Manager (NPM) | npm | Latest |

## 8. Implementation Order

The projects have clear dependencies that dictate build order:

### Phase 1: KoreForge.Scripts Library
**No dependencies on other new projects.**

1. KF.Scripts.Abstractions — Models, interfaces, options, exceptions
2. KF.Scripts.Data — SQL implementation (Scripts + ScriptHistory tables)
3. KF.Scripts.Core — ScriptStore, ScriptHistoryService, ScriptChangeMonitor, ScriptValidator
4. KF.Scripts.AspNet — Endpoint mappings, request/response DTOs
5. KF.Scripts — Bundler package
6. KF.Scripts.Cli — CLI tool
7. KF.Scripts.Tests — Unit tests (target: 70%+ coverage)

**Deliverable:** Working NuGet package with CLI, tested against SQL Server.

### Phase 2: KafkaProcessor Backend Extensions
**Depends on Phase 1.**

1. JexScriptCompiler — Bridge KoreForge.Scripts IScriptCompiler to KoreForge.Jex
2. FunctionScripts table + FunctionScriptRepository
3. Updated FunctionDefinitionLoader — Load from Scripts table via FunctionScripts junction
4. ScriptReloadService — Subscribe to change notifications, hot-swap compiled scripts
5. Function API controllers
6. Script migration tooling — Generate initial scripts from FieldExtractionRules using JexScriptGenerator
7. Program.cs integration — Register services, map endpoints, add SignalR hubs
8. ShadowTestService + ShadowTestHub (can be deferred to Phase 2b)
9. Update existing unit tests, add new tests for reload and API

**Deliverable:** KafkaProcessor running with database-backed scripts, hot-reload working.

### Phase 3: KF.Vue.Scripts Component Library
**No backend dependency (works against any KoreForge.Scripts API).**

1. Project scaffolding — Vite library mode, TypeScript, ESLint
2. Types — TypeScript interfaces mirroring C# models
3. useScriptApi composable — HTTP client
4. useScriptEditor composable — Edit lifecycle, auto-compile
5. useScriptHistory composable — History, diff, rollback
6. useScriptTester composable — Test execution
7. useSignalRStream composable — SignalR connection management
8. KfScriptEditor component — Monaco integration, toolbar, status
9. KfHistoryViewer component — Timeline, diff viewer
10. KfRollbackDialog component — Confirmation dialog
11. KfScriptTester component — Side-by-side input/output
12. KfCompileStatus component — Error/warning display
13. Default theme CSS
14. Package build and publish

**Deliverable:** Published NPM package, installable in any Vue 3 app.

### Phase 4: KafkaProcessor Dashboard
**Depends on Phase 2 (backend) and Phase 3 (component library).**

1. Project scaffolding — Vite + Vue 3 + Vue Router + TypeScript
2. Application layout — Sidebar navigation, connection status
3. DashboardView — Overview panels
4. FunctionsView + FunctionDetailView — Function management
5. ScriptsView + ScriptDetailView — Script management (uses KF.Vue.Scripts)
6. SettingsView — Application-aware settings forms
7. MetricsView — Real-time charts (SignalR)
8. ShadowTestView — Shadow testing (SignalR streaming)

**Deliverable:** Fully operational dashboard served from KafkaProcessor.

## 9. Operational Workflows

### For a developer updating an extraction script:

1. Open `extract-login.jex` in VS Code, edit with full language server support
2. Test locally: `jex extract-login.jex --input sample.json`
3. Upload: `kf-scripts upload extract-login --file extract-login.jex --comment "Fixed amount casing"`
4. Script compiles successfully on the server, old version archived
5. Within 30 seconds, KafkaProcessor hot-reloads the new script
6. If something is wrong: `kf-scripts rollback extract-login 0` — instant rollback

### For an operator using the dashboard:

1. Open KafkaProcessor Dashboard in browser
2. Navigate to Functions → Login → click "Edit Script"
3. Modify the script in the Monaco editor
4. See real-time compile errors as you type
5. Click "Test" → paste a sample message → see extraction output
6. Click "Shadow Test" → watch live messages being processed with both scripts
7. Satisfied → click "Promote" → new script goes live
8. Problem found → click "Rollback" → select previous version → restored in seconds

### For a new function deployment:

1. Create function definition via dashboard (name, regex, group)
2. Write extraction script in VS Code with language server
3. Upload script: `kf-scripts create extract-new-function --file new.jex --type extract`
4. Assign script to function via dashboard: Functions → New Function → Assign Script
5. Function starts processing matching messages with the new script

## 10. Cross-Cutting Concerns

### Logging

All operations use KoreForge.Logging patterns with source-generated event IDs:
- Script CRUD operations → log at Information level
- Compilation failures → log at Warning level
- Reload events → log at Information level
- Shadow test sessions → log at Information level
- Polling failures → log at Error level

### Metrics

KoreForge.Metrics instrumentation for:
- `scripts.compile` — compilation count, success/fail, duration
- `scripts.reload` — reload count, duration
- `scripts.shadow_test` — session count, result count
- `scripts.api` — endpoint call count, latency

### Health Checks

| Check | Type | Library |
|-------|------|---------|
| `scripts-loaded` | Readiness | KafkaProcessor |
| `scripts-poll` | Liveness | KoreForge.Scripts |
| `scripts-compile` | Liveness | KoreForge.Scripts |

### Error Handling

| Scenario | Behavior |
|----------|----------|
| Bad script uploaded | 400 with `CompilationResult` diagnostics |
| RowVersion conflict | 409 with `ScriptConcurrencyException` details |
| Rollback conflict | 409 with `RollbackConflictException` details |
| Script poll failure | Degraded health, log error, retry next interval |
| Reload compilation failure | Keep previous version, log warning, health degraded |
| Shadow test candidate crash | Catch exception, send `shadow-error`, continue live pipeline |
| Shadow session timeout | Send `shadow-complete` with reason `timeout` |

## 11. File Structure Summary

```
KoreForge/                                       (workspace root)
│
├── KoreForge.Scripts/                           ← repo: koreforger/KoreForge.Scripts
│   ├── KoreForge.Scripts.slnx                       lib (nuget/koreforge)
│   ├── Directory.Build.props
│   ├── Directory.Packages.props
│   ├── coverlet.runsettings
│   ├── LICENSE.md
│   ├── README.md
│   ├── src/
│   │   ├── KF.Scripts/                          [Bundler package]
│   │   ├── KF.Scripts.Abstractions/             [Interfaces, models]
│   │   ├── KF.Scripts.Core/                     [Services]
│   │   ├── KF.Scripts.Data/                     [SQL implementation]
│   │   ├── KF.Scripts.AspNet/                   [ASP.NET endpoints]
│   │   └── KF.Scripts.Cli/                      [CLI tool]
│   ├── tst/
│   │   ├── KF.Scripts.Tests/
│   │   └── KF.Scripts.Benchmarks/
│   ├── scr/                                     [Build/test/publish scripts]
│   ├── doc/
│   │   ├── Specification.md                     [Library spec]
│   │   └── Main-Specification.md                [THIS DOCUMENT]
│   └── artifacts/                               [NuGet output]
│
├── KoreForge.Vue.Scripts/                       ← repo: koreforger/KoreForge.Vue.Scripts
│   ├── package.json                                weblib (npm/koreforge)
│   ├── tsconfig.json
│   ├── vite.config.ts
│   ├── LICENSE.md
│   ├── README.md
│   ├── src/
│   │   ├── index.ts
│   │   ├── composables/                         [Headless logic]
│   │   ├── components/                          [Themed Vue components]
│   │   ├── theme/                               [CSS custom properties]
│   │   └── types/                               [TypeScript definitions]
│   ├── doc/
│   │   └── Specification.md
│   └── scripts/
│
├── KafkaProcessor.Scripts.API/                  ← repo: koreforger/KafkaProcessor.Scripts.API
│   ├── KafkaProcessor.Scripts.API.slnx              app (kafkaprocessor)
│   ├── Directory.Build.props
│   ├── README.md
│   ├── src/
│   │   └── KafkaProcessor.Scripts.API/
│   │       ├── Controllers/                     [FunctionsController, ShadowTestController]
│   │       ├── Hubs/                            [ShadowTestHub]
│   │       ├── Services/                        [ScriptReload, ShadowTest, FunctionScriptRepo]
│   │       └── Models/                          [DTOs, junction models]
│   ├── tst/
│   │   └── KafkaProcessor.Scripts.API.Tests/
│   ├── scr/                                     [Build/test scripts]
│   ├── doc/
│   │   └── Specification.md
│   └── artifacts/
│
├── KafkaProcessor.Dashboard/                    ← repo: koreforger/KafkaProcessor.Dashboard
│   ├── package.json                                web (kafkaprocessor)
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── index.html
│   ├── src/
│   │   ├── App.vue
│   │   ├── main.ts
│   │   ├── router.ts
│   │   ├── composables/                         [useFunctions, useShadowTest, useMetrics, useSettings]
│   │   ├── components/                          [App-specific components]
│   │   └── views/                               [Route views]
│   └── doc/
│       └── Specification.md
│
├── apps/
│   └── KafkaProcessor/                          ← existing: consumes above packages
│       ├── src/KafkaProcessor/
│       │   ├── Program.cs                       [Wires everything together]
│       │   └── ...existing pipeline code...
│       └── doc/
│           └── Scripts-API-Specification.md     [Backend integration spec]
```

## 12. Specifications Index

| Document | Location | Scope |
|----------|----------|-------|
| **Main Specification** (this document) | [KoreForge.Scripts/doc/Main-Specification.md](Main-Specification.md) | Cross-project architecture, data flows, implementation order |
| **KoreForge.Scripts Spec** | [KoreForge.Scripts/doc/Specification.md](Specification.md) | Library: schema, interfaces, services, CLI, build/packaging |
| **KoreForge.Vue.Scripts Spec** | [KoreForge.Vue.Scripts/doc/Specification.md](../../KoreForge.Vue.Scripts/doc/Specification.md) | NPM package: composables, components, theming, build |
| **KafkaProcessor.Dashboard Spec** | [KafkaProcessor.Dashboard/doc/Specification.md](../../KafkaProcessor.Dashboard/doc/Specification.md) | SPA: views, routes, layout, data sources |
| **KafkaProcessor.Scripts.API Spec** | [KafkaProcessor.Scripts.API/doc/Specification.md](../../KafkaProcessor.Scripts.API/doc/Specification.md) | Backend: endpoints, hubs, services, migration |
| **Integration Spec** | [apps/KafkaProcessor/doc/Scripts-API-Specification.md](../../apps/KafkaProcessor/doc/Scripts-API-Specification.md) | How KafkaProcessor wires everything together |
