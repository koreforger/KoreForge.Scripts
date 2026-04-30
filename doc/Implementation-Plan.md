# Implementation Plan — KoreForge Scripts Feature

Detailed step-by-step build plan for all 4 repositories, covering project scaffolding, design, implementation, testing, code coverage, build scripts, and package publishing.

---

## Phase 1: KoreForge.Scripts (NuGet Library)

**Repo:** `koreforger/KoreForge.Scripts`
**Folder:** `KoreForge.Scripts/`
**Dependency:** None (this is the foundation)

### Step 1.1 — Solution & Build Infrastructure

| # | Task | Details |
|---|------|---------|
| 1 | Create `KoreForge.Scripts.slnx` | Solution with `src/` and `tst/` solution folders |
| 2 | Create `Directory.Build.props` | Product=KoreForge.Scripts, Authors=KoreForge, Company=KoreForge, RepositoryUrl, MinVerTagPrefix=`Scripts/v`, MinVerAutoIncrement=minor, MinVerDefaultPreReleaseIdentifiers=alpha.0, TreatWarningsAsErrors=true, Nullable=enable, ImplicitUsings=enable, GenerateDocumentationFile=true, LangVersion=latest, PackageOutputPath=artifacts/ |
| 3 | Create `Directory.Packages.props` | ManagePackageVersionsCentrally=true. Pin: MinVer 6.0.0, Microsoft.Data.SqlClient 6.0.0, Microsoft.Extensions.* 10.0.0, Microsoft.AspNetCore.* 10.0.0, xunit 2.9.3, xunit.runner.visualstudio 3.0.2, coverlet.collector 6.0.4, FluentAssertions 8.0.0, Moq 4.20.72, BenchmarkDotNet 0.15.8, System.CommandLine 2.0.0-beta4 |
| 4 | Create `coverlet.runsettings` | Format=cobertura, Exclude=[*.Tests]*,[*.Benchmarks]*,[*.Sample]*, ExcludeByAttribute=GeneratedCodeAttribute,CompilerGeneratedAttribute |
| 5 | Create `LICENSE.md` | MIT license |
| 6 | Create `.gitignore` | Standard .NET gitignore + artifacts/, out/, TestResults/ |

### Step 1.2 — KF.Scripts.Abstractions

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KF.Scripts.Abstractions.csproj` | net10.0, IsPackable=false, RootNamespace=KF.Scripts.Abstractions |
| 2 | Models | `Models/ScriptRecord.cs` | sealed record: ScriptId, ApplicationId, Name, TypeTag, Language, Content, Description, IsEnabled, CreatedBy/Date, ModifiedBy/Date, Comment, RowVersion |
| 3 | Models | `Models/ScriptHistoryRecord.cs` | sealed record: HistoryId, ScriptId, ApplicationId, Name, OldContent, NewContent, OldIsEnabled, NewIsEnabled, RowVersionBefore/After, ChangedBy, ChangedDate, Operation, Comment |
| 4 | Models | `Models/ScriptOperation.cs` | enum: Insert, Update, Delete, Rollback |
| 5 | Models | `Models/CompilationResult.cs` | sealed record: Success, Diagnostics list |
| 6 | Models | `Models/CompilationDiagnostic.cs` | sealed record: Severity, Message, Line?, Column? |
| 7 | Models | `Models/DiagnosticSeverity.cs` | enum: Info, Warning, Error |
| 8 | Requests | `Models/CreateScriptRequest.cs` | sealed record: Name, TypeTag, Language, Content, Description, CreatedBy, Comment |
| 9 | Requests | `Models/UpdateScriptRequest.cs` | sealed record: ScriptId, Content, Description, RowVersion, ModifiedBy, Comment |
| 10 | Options | `ScriptStoreOptions.cs` | ConnectionString, Application, PollingInterval, MaxContentLength |
| 11 | Interfaces | `Interfaces/IScriptStore.cs` | ListAsync, GetByIdAsync, GetByNameAsync, CreateAsync, UpdateAsync, DeleteAsync |
| 12 | Interfaces | `Interfaces/IScriptHistoryService.cs` | GetHistoryAsync, RollbackAsync |
| 13 | Interfaces | `Interfaces/IScriptCompiler.cs` | Language property, CompileAsync(content) |
| 14 | Interfaces | `Interfaces/IScriptValidator.cs` | ValidateAsync(content, language) |
| 15 | Interfaces | `Interfaces/IScriptChangeNotification.cs` | ScriptsChanged event, ScriptChangeEvent record |
| 16 | Exceptions | `Exceptions/ScriptCompilationException.cs` | Contains CompilationResult |
| 17 | Exceptions | `Exceptions/ScriptConcurrencyException.cs` | ScriptId, Name |
| 18 | Exceptions | `Exceptions/RollbackConflictException.cs` | Name, VersionIndex |

### Step 1.3 — KF.Scripts.Data

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KF.Scripts.Data.csproj` | net10.0, IsPackable=false, depends on KF.Scripts.Abstractions + Microsoft.Data.SqlClient |
| 2 | SQL scripts | `Sql/CreateScriptsTable.sql` | Scripts table DDL (as specified) |
| 3 | SQL scripts | `Sql/CreateScriptHistoryTable.sql` | ScriptHistory table DDL (as specified) |
| 4 | Repository | `ScriptDataRepository.cs` | Raw ADO.NET (no EF). Methods: Insert, Update, Delete, GetById, GetByName, List, GetAll. Uses parameterized queries. UPDLOCK for concurrency. |
| 5 | Repository | `ScriptHistoryDataRepository.cs` | Insert history record, GetByScriptId, GetByName. Ordered by ChangedDate DESC. |

### Step 1.4 — KF.Scripts.Core

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KF.Scripts.Core.csproj` | net10.0, IsPackable=false, depends on Abstractions + Data + Microsoft.Extensions.Hosting + Microsoft.Extensions.Options + Microsoft.Extensions.DependencyInjection.Abstractions |
| 2 | Service | `Services/ScriptStore.cs` | Implements IScriptStore. Create: validate → resolve IScriptCompiler by language → compile → insert → history. Update: UPDLOCK + RowVersion check → compile → update → history. Delete: UPDLOCK → delete → history. |
| 3 | Service | `Services/ScriptHistoryService.cs` | Implements IScriptHistoryService. GetHistory: delegate to data repo. Rollback: fetch history → validate index → check RowVersion conflict → compile restored content → update → history (Operation=Rollback). |
| 4 | Service | `Services/ScriptValidator.cs` | Implements IScriptValidator. Resolves IScriptCompiler by language from DI (keyed services). Delegates CompileAsync. |
| 5 | Service | `Services/ScriptChangeMonitor.cs` | Implements IScriptChangeNotification + IHostedService. Poll loop: load all scripts for ApplicationId → compare RowVersion dictionary → fire ScriptsChanged event on delta. Sleep PollingInterval. Error → log + continue. |
| 6 | DI | `ServiceCollectionExtensions.cs` | AddKoreForgeScripts(Action<ScriptStoreOptions>), AddScriptCompiler<T>(language) using keyed services |

### Step 1.5 — KF.Scripts.AspNet

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KF.Scripts.AspNet.csproj` | net10.0, IsPackable=false, depends on Core + Microsoft.AspNetCore.App (framework ref) |
| 2 | Endpoints | `ScriptEndpoints.cs` | MapScriptEndpoints() extension. Minimal API handlers for: GET / (list), GET /{id} (get), GET /by-name/{name}, POST / (create), PUT /{id} (update), DELETE /{id}, GET /{id}/history, POST /by-name/{name}/rollback, POST /validate, POST /{id}/test |
| 3 | DTOs | `Dtos/CreateScriptDto.cs` | Request body DTOs (camelCase). Maps to CreateScriptRequest. |
| 4 | DTOs | `Dtos/UpdateScriptDto.cs` | Request body DTO with rowVersion as base64 string |
| 5 | DTOs | `Dtos/TestScriptDto.cs` | { input: object, scriptId?: long, content?: string } |
| 6 | DTOs | `Dtos/TestScriptResultDto.cs` | { success, output, diagnostics, executionTimeMs } |
| 7 | Error handling | `ScriptProblemDetails.cs` | Maps exceptions to Problem Details (400/409) |

### Step 1.6 — KF.Scripts (Bundler)

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KF.Scripts.csproj` | PackageId=KoreForge.Scripts, IncludeBuildOutput=false, TargetsForTfmSpecificBuildOutput bundles all 4 DLLs + XMLs. Same bundling pattern as KoreForge.Settings and KoreForge.Processing. |

### Step 1.7 — KF.Scripts.Cli

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KF.Scripts.Cli.csproj` | OutputType=Exe, PackAsTool=true, ToolCommandName=kf-scripts, depends on Core |
| 2 | Program | `Program.cs` | System.CommandLine root command with global options: --connection, --application, --format |
| 3 | Commands | `Commands/ListCommand.cs` | `kf-scripts list [--type] [--enabled]` → tabular output |
| 4 | Commands | `Commands/GetCommand.cs` | `kf-scripts get <id>` → JSON output |
| 5 | Commands | `Commands/DownloadCommand.cs` | `kf-scripts download <name> [--output file.jex]` → file or stdout |
| 6 | Commands | `Commands/UploadCommand.cs` | `kf-scripts upload <name> --file <path> --type <tag>` → validate + create/update |
| 7 | Commands | `Commands/CreateCommand.cs` | `kf-scripts create <name> --file <path> --type <tag>` → create only |
| 8 | Commands | `Commands/DeleteCommand.cs` | `kf-scripts delete <id> --rowversion <hex>` |
| 9 | Commands | `Commands/HistoryCommand.cs` | `kf-scripts history <name>` → table of versions |
| 10 | Commands | `Commands/RollbackCommand.cs` | `kf-scripts rollback <name> <versionIndex>` |
| 11 | Commands | `Commands/ValidateCommand.cs` | `kf-scripts validate --file <path>` → compile check |
| 12 | Commands | `Commands/ExportCommand.cs` | `kf-scripts export --output <file> [--type]` → JSON |
| 13 | Commands | `Commands/ImportCommand.cs` | `kf-scripts import --file <path> [--apply] [--upsert]` |

### Step 1.8 — KF.Scripts.Tests

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KF.Scripts.Tests.csproj` | IsPackable=false, IsTestProject=true, depends on Core + AspNet + xunit + FluentAssertions + Moq + coverlet |
| 2 | Tests | `ScriptStoreTests.cs` | Create (happy), Create with compilation error (rejected), Update (happy), Update with wrong RowVersion (409), Delete (happy). Mock IScriptCompiler, mock data repos. |
| 3 | Tests | `ScriptHistoryServiceTests.cs` | GetHistory (returns ordered list), Rollback (happy), Rollback with conflict (throws), Rollback out of range (throws). |
| 4 | Tests | `ScriptChangeMonitorTests.cs` | Detect added script, detect changed script, detect deleted script, no change = no event. Using fake timers. |
| 5 | Tests | `ScriptValidatorTests.cs` | Resolves correct compiler by language, returns result, throws for unknown language. |
| 6 | Tests | `ScriptEndpointTests.cs` | Integration tests using WebApplicationFactory. Test all endpoints: list, get, create, update, delete, history, rollback, validate. |
| 7 | Tests | `ScriptDataRepositoryTests.cs` | Against real SQL (or LocalDB). Insert, select, update, delete, concurrency. |

**Coverage target:** 70%+

### Step 1.9 — Build Scripts (scr/)

| # | Script | Details |
|---|--------|---------|
| 1 | `build-test.ps1` | `dotnet build KoreForge.Scripts.slnx -c $Configuration; dotnet test KoreForge.Scripts.slnx -c $Configuration --no-build` |
| 2 | `build-test-codecoverage.ps1` | Build → test with coverlet → reportgenerator HTML report to `out/TestResults/coverage/` |
| 3 | `build-rebuild.ps1` | `dotnet build --force -c $Configuration` |
| 4 | `build-benchmark.ps1` | `dotnet run --project tst/KF.Scripts.Benchmarks -c Release -- --filter *` |
| 5 | `git-push.ps1` | `git add -A; git commit; git push` |
| 6 | `git-push-nuget.ps1` | Tag `Scripts/v$Version` → push tag → triggers CI/NuGet publish |

### Step 1.10 — Validation & Ship

| # | Task | Details |
|---|------|---------|
| 1 | Run all tests | `scr/build-test.ps1` — all green |
| 2 | Run coverage | `scr/build-test-codecoverage.ps1` — verify 70%+ |
| 3 | Pack NuGet | `dotnet pack src/KF.Scripts/KF.Scripts.csproj -c Release` → artifacts/KoreForge.Scripts.0.0.1-alpha.nupkg |
| 4 | Pack CLI tool | `dotnet pack src/KF.Scripts.Cli/KF.Scripts.Cli.csproj -c Release` → artifacts/KoreForge.Scripts.Cli.0.0.1-alpha.nupkg |
| 5 | Test CLI locally | `dotnet tool install --global --add-source artifacts KoreForge.Scripts.Cli` → `kf-scripts list --connection "..." --application Test` |
| 6 | Tag & push | `scr/git-push-nuget.ps1 -Version 0.0.1-alpha` |
| 7 | Push to NuGet | `dotnet nuget push artifacts/*.nupkg --source https://api.nuget.org/v3/index.json --api-key $KEY` |

---

## Phase 2: KafkaProcessor.Scripts.API (NuGet Library)

**Repo:** `koreforger/KafkaProcessor.Scripts.API`
**Folder:** `KafkaProcessor.Scripts.API/`
**Dependency:** KoreForge.Scripts (Phase 1), KoreForge.Jex

### Step 2.1 — Solution & Build Infrastructure

| # | Task | Details |
|---|------|---------|
| 1 | Create `KafkaProcessor.Scripts.API.slnx` | Solution with src/ and tst/ folders |
| 2 | Create `Directory.Build.props` | Product=KafkaProcessor.Scripts.API, RepositoryUrl, MinVerTagPrefix=`KafkaProcessor.Scripts.API/v`, TreatWarningsAsErrors, Nullable, ImplicitUsings, GenerateDocumentationFile |
| 3 | Create `Directory.Packages.props` | Pin: KoreForge.Scripts 0.0.1-alpha, KoreForge.Jex 0.0.2-alpha, Microsoft.AspNetCore.SignalR.* 10.0.0, Microsoft.Extensions.* 10.0.0, + test deps |
| 4 | Create `coverlet.runsettings` | Same pattern as KoreForge libraries |
| 5 | Create `LICENSE.md`, `.gitignore` | MIT, standard .NET gitignore |

### Step 2.2 — KafkaProcessor.Scripts.API Project

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KafkaProcessor.Scripts.API.csproj` | net10.0, IsPackable=true, PackageId=KafkaProcessor.Scripts.API, depends on KoreForge.Scripts + KoreForge.Jex + Microsoft.AspNetCore.App (framework ref) |
| 2 | Models | `Models/FunctionDefinition.cs` | FunctionId, FunctionName, ActionRegex, GroupId, GroupName, IsEnabled (same as existing KafkaProcessor model) |
| 3 | Models | `Models/FunctionScriptAssignment.cs` | FunctionId, ScriptId, Role, Ordinal, IsEnabled |
| 4 | Models | `Models/ShadowTestSession.cs` | SessionId, FunctionId, CandidateProgram, CurrentProgram, SampleSize, Remaining, TimeoutAt, StartedAt |
| 5 | Models | `Models/ShadowTestResult.cs` | SessionId, MessageIndex, InputSnippet, CurrentOutput, CandidateOutput, Diffs, Timestamps |
| 6 | Models | `Models/ShadowTestSummary.cs` | SessionId, TotalProcessed, MatchCount, DiffCount, AvgTimes, CompletedAt, Reason |
| 7 | Services | `Services/FunctionScriptRepository.cs` | ADO.NET CRUD for FunctionScripts junction table. GetAssignmentsForFunction, GetAssignmentsForScript, ReplaceAssignments. |
| 8 | Services | `Services/FunctionDefinitionRepository.cs` | ADO.NET CRUD for FunctionDefinitions. LoadAll, GetById, Create, Update, Delete. (Extended from existing KafkaProcessor code.) |
| 9 | Services | `Services/JexScriptCompiler.cs` | Implements IScriptCompiler (from KoreForge.Scripts). Language="jex". Bridges to KoreForge.Jex parser/compiler. Returns CompilationResult with diagnostics. |
| 10 | Services | `Services/ScriptReloadService.cs` | IHostedService. Subscribes to IScriptChangeNotification. On change: load script → compile via JEX → atomic swap of FrozenDictionary<long, CompiledFunction>. Log reload events. |
| 11 | Services | `Services/ShadowTestService.cs` | Manages ConcurrentDictionary<string, ShadowSession>. Start/Stop/Promote sessions. Max sessions configurable. Auto-expire on timeout. |
| 12 | Controllers | `Controllers/FunctionEndpoints.cs` | MapFunctionEndpoints(): GET/POST/PUT/DELETE /api/functions, GET/PUT /api/functions/{id}/scripts |
| 13 | Controllers | `Controllers/ShadowTestEndpoints.cs` | MapShadowTestEndpoints(): POST /api/shadow-test, GET/DELETE /api/shadow-test/{sessionId}, POST /api/shadow-test/{sessionId}/promote |
| 14 | Hubs | `Hubs/ShadowTestHub.cs` | Client methods: JoinSession, LeaveSession. Server events: shadow-result, shadow-complete, shadow-error. Uses groups per sessionId. |
| 15 | Options | `ScriptsApiOptions.cs` | MaxShadowTestSessions (default 5), ShadowTestTimeout (default 5min) |
| 16 | DI | `ServiceCollectionExtensions.cs` | AddKafkaProcessorScriptsApi(Action<ScriptsApiOptions>) — registers all services, repos, JexScriptCompiler |
| 17 | SQL | `Sql/CreateFunctionScriptsTable.sql` | Junction table DDL |

### Step 2.3 — KafkaProcessor.Scripts.API.Tests

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create csproj | `KafkaProcessor.Scripts.API.Tests.csproj` | net10.0, IsPackable=false, IsTestProject=true |
| 2 | Tests | `JexScriptCompilerTests.cs` | Valid JEX compiles successfully, invalid JEX returns diagnostics with line numbers |
| 3 | Tests | `FunctionScriptRepositoryTests.cs` | Assign, replace, unassign scripts from functions |
| 4 | Tests | `ScriptReloadServiceTests.cs` | Receives change event → recompiles → swaps dictionary. Failed compilation → keeps old version. |
| 5 | Tests | `ShadowTestServiceTests.cs` | Start session, max sessions enforced, timeout expires, promote saves script. Mock IScriptStore. |
| 6 | Tests | `FunctionEndpointTests.cs` | WebApplicationFactory integration tests for all function endpoints |
| 7 | Tests | `ShadowTestEndpointTests.cs` | Start/stop/promote shadow test endpoints |

**Coverage target:** 70%+

### Step 2.4 — Build Scripts (scr/)

| # | Script | Details |
|---|--------|---------|
| 1 | `build-test.ps1` | Build + test |
| 2 | `build-test-codecoverage.ps1` | Build + test + coverage HTML report |
| 3 | `git-push-nuget.ps1` | Tag `KafkaProcessor.Scripts.API/v$Version` → push |

### Step 2.5 — Validation & Ship

| # | Task | Details |
|---|------|---------|
| 1 | Run all tests | All green |
| 2 | Check coverage | 70%+ |
| 3 | Pack NuGet | `dotnet pack -c Release` → KafkaProcessor.Scripts.API.0.0.1-alpha.nupkg |
| 4 | Tag & push | `scr/git-push-nuget.ps1 -Version 0.0.1-alpha` |
| 5 | Push to NuGet | `dotnet nuget push artifacts/*.nupkg ...` |

---

## Phase 3: KoreForge.Vue.Scripts (NPM Package)

**Repo:** `koreforger/KoreForge.Vue.Scripts`
**Folder:** `KoreForge.Vue.Scripts/`
**Dependency:** None (works against any KoreForge.Scripts API)

### Step 3.1 — Project Scaffolding

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create `package.json` | `package.json` | name=@koreforge/vue-scripts, version=0.1.0, type=module, private=false. peerDependencies: vue ^3.5.0, @microsoft/signalr ^8.0.0, monaco-editor ^0.48.0. devDependencies: @vitejs/plugin-vue, typescript ~5.7, vue-tsc, vite ^6.2.0, vitest, @vue/test-utils, msw, eslint, @typescript-eslint/* |
| 2 | Create `tsconfig.json` | `tsconfig.json` | strict=true, target=ESNext, module=ESNext, moduleResolution=bundler, jsx=preserve, paths: @/* → src/* |
| 3 | Create `vite.config.ts` | `vite.config.ts` | Library mode: entry=src/index.ts, formats=[es, cjs], external=[vue, @microsoft/signalr, monaco-editor]. Vue plugin. |
| 4 | Create `.eslintrc.cjs` | `.eslintrc.cjs` | @typescript-eslint + vue recommended |
| 5 | Create `.gitignore` | `.gitignore` | node_modules/, dist/, coverage/ |
| 6 | Create `LICENSE.md` | `LICENSE.md` | MIT |

### Step 3.2 — Types

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Types | `src/types/index.ts` | TypeScript interfaces mirroring all C# models: ScriptRecord, ScriptHistoryRecord, CompilationResult, CompilationDiagnostic, CreateScriptRequest, UpdateScriptRequest, TestScriptRequest, TestScriptResult. Plus enums: ScriptOperation, DiagnosticSeverity, ConnectionState. |

### Step 3.3 — Composables

| # | Task | File | Details |
|---|------|------|---------|
| 1 | useScriptApi | `src/composables/useScriptApi.ts` | HTTP client wrapping fetch(). All endpoints: list, getById, getByName, create, update, del, getHistory, rollback, validate, test. Consistent error handling. |
| 2 | useScriptEditor | `src/composables/useScriptEditor.ts` | Reactive state: content, originalContent, isDirty, isLoading, isSaving, isCompiling, compilationResult, script, error. Actions: load, compile (debounced), save, reset. Auto-compile on content change. |
| 3 | useScriptHistory | `src/composables/useScriptHistory.ts` | State: history, isLoading, selectedVersion, diffResult. Actions: load, selectVersion, rollback. |
| 4 | useScriptTester | `src/composables/useScriptTester.ts` | State: input, output, isRunning, executionTimeMs, error, diagnostics. Actions: run(scriptId?, content?), clear. |
| 5 | useSignalRStream | `src/composables/useSignalRStream.ts` | State: connectionState, error. Actions: connect, disconnect, on/off, invoke, joinGroup, leaveGroup. Auto-reconnect with exponential backoff. |

### Step 3.4 — Theme

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Default theme | `src/theme/default.css` | All CSS custom properties (--kf-*) as specified. Clean, neutral defaults. |
| 2 | Dark theme | `src/theme/dark.css` | Dark mode overrides (inverted surfaces, adjusted accents). |

### Step 3.5 — Components

| # | Task | File | Details |
|---|------|------|---------|
| 1 | KfScriptEditor | `src/components/KfScriptEditor.vue` | Monaco editor wrapper. Props: scriptId, scriptName, apiBaseUrl, language, height, readOnly, autoCompileMs, monacoOptions. Slots: toolbar, status, error. Emits: saved, compiled, error, dirty-change. Register JEX TextMate grammar. Keyboard shortcuts: Ctrl+S, Ctrl+Shift+B. |
| 2 | KfHistoryViewer | `src/components/KfHistoryViewer.vue` | Timeline list of ScriptHistoryRecords. Operation badges (color-coded). Click to select → Monaco diff editor shows side-by-side. Rollback button per entry. |
| 3 | KfRollbackDialog | `src/components/KfRollbackDialog.vue` | Modal overlay. Shows diff + metadata. Confirm/Cancel buttons. Loading state during rollback. |
| 4 | KfScriptTester | `src/components/KfScriptTester.vue` | Two-panel layout. Left: Monaco JSON editor (input). Right: Monaco JSON read-only (output). Run button. Execution time. Error panel below. |
| 5 | KfCompileStatus | `src/components/KfCompileStatus.vue` | Compact status bar. Green check / red error count / yellow warning count. Expandable error list. Emits goto-line events. |

### Step 3.6 — Package Entry Point

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Index | `src/index.ts` | Export all composables, components, types, and registerJexLanguage(monaco) utility. |

### Step 3.7 — Tests

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Composable tests | `src/__tests__/useScriptApi.test.ts` | Mock fetch with MSW. Test all API methods: list, get, create, update, delete, history, rollback, validate. |
| 2 | Composable tests | `src/__tests__/useScriptEditor.test.ts` | Load, dirty tracking, auto-compile debounce, save flow, reset. |
| 3 | Composable tests | `src/__tests__/useScriptHistory.test.ts` | Load history, select version, rollback call. |
| 4 | Composable tests | `src/__tests__/useScriptTester.test.ts` | Run with scriptId, run with content, invalid JSON rejection. |
| 5 | Component tests | `src/__tests__/KfCompileStatus.test.ts` | Renders success, renders errors, emits goto-line. |

### Step 3.8 — Build Scripts

| # | Script | File | Details |
|---|--------|------|---------|
| 1 | Build | `scripts/build.ps1` | `npm run build` (Vite library mode) |
| 2 | Test | `scripts/test.ps1` | `npm run test` (Vitest) |
| 3 | Lint | `scripts/lint.ps1` | `npm run lint` |
| 4 | Publish | `scripts/publish.ps1` | `npm version $Version; npm publish --access public` |

### Step 3.9 — Validation & Ship

| # | Task | Details |
|---|------|---------|
| 1 | Run tests | `npm test` — all green |
| 2 | Lint | `npm run lint` — clean |
| 3 | Type check | `npm run typecheck` — no errors |
| 4 | Build | `npm run build` — dist/ produced with .js, .cjs, .d.ts, .css |
| 5 | Test local install | Create a throwaway Vue app, `npm install ../KoreForge.Vue.Scripts`, import and render `<KfScriptEditor>` |
| 6 | Publish | `npm publish --access public` → @koreforge/vue-scripts@0.1.0 on npmjs.com |

---

## Phase 4: KafkaProcessor.Dashboard (Vue 3 SPA)

**Repo:** `koreforger/KafkaProcessor.Dashboard`
**Folder:** `KafkaProcessor.Dashboard/`
**Dependency:** KoreForge.Vue.Scripts (Phase 3), KafkaProcessor.Scripts.API running (Phase 2)

### Step 4.1 — Project Scaffolding

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Create `package.json` | `package.json` | name=kafkaprocessor-dashboard, private=true, type=module. dependencies: vue ^3.5.0, vue-router ^4.5.0, @koreforge/vue-scripts ^0.1.0, @microsoft/signalr ^8.0.0, monaco-editor ^0.48.0. devDependencies: @vitejs/plugin-vue, typescript ~5.7, vue-tsc, vite ^6.2.0, @types/node. scripts: dev, build, preview, typecheck. |
| 2 | Create `vite.config.ts` | `vite.config.ts` | Vue plugin, alias @/ → src/, dev proxy: /api → localhost:5000, /hub → localhost:5000 (ws:true) |
| 3 | Create `tsconfig.json` | `tsconfig.json` | Strict, paths: @/* → src/* |
| 4 | Create `index.html` | `index.html` | Standard Vite entry, `<div id="app">`, script src=/src/main.ts |
| 5 | Create `.gitignore` | `.gitignore` | node_modules/, dist/ |

### Step 4.2 — App Shell

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Main entry | `src/main.ts` | createApp(App), use(router), mount('#app'), import default theme CSS |
| 2 | App shell | `src/App.vue` | Sidebar nav + `<router-view>`. Connection status badge in header. |
| 3 | Router | `src/router.ts` | Routes as specified: /, /functions, /functions/:id, /scripts, /scripts/:id, /rules, /shadow-test, /settings, /metrics |
| 4 | Global styles | `src/style.css` | Layout grid, sidebar styles. KF CSS variable overrides for app theme. |
| 5 | Types | `src/types.ts` | App-specific types: FunctionDefinition, FunctionWithScripts, MetricsSnapshot, SettingEntry, ShadowTestResult, etc. |

### Step 4.3 — Composables (App-Specific)

| # | Task | File | Details |
|---|------|------|---------|
| 1 | useFunctions | `src/composables/useFunctions.ts` | CRUD for /api/functions. Load, getById, update, assignScript, unassignScript. |
| 2 | useShadowTest | `src/composables/useShadowTest.ts` | Start/stop/promote sessions. SignalR subscription via useSignalRStream. Live results array. |
| 3 | useMetrics | `src/composables/useMetrics.ts` | Subscribe to /hub/metrics. Rolling buffer of 300 snapshots. |
| 4 | useSettings | `src/composables/useSettings.ts` | GET/PUT /api/settings. Subscribe to /hub/settings for live changes. |

### Step 4.4 — Views

| # | Task | File | Details |
|---|------|------|---------|
| 1 | DashboardView | `src/views/DashboardView.vue` | Summary panels: function count, script count, live throughput, recent activity |
| 2 | FunctionsView | `src/views/FunctionsView.vue` | Table of all functions. Sortable, filterable. Link to detail. |
| 3 | FunctionDetailView | `src/views/FunctionDetailView.vue` | Function metadata + embedded KfScriptEditor for extract script + KfScriptTester |
| 4 | ScriptsView | `src/views/ScriptsView.vue` | Table of all scripts. Filter by TypeTag. Link to detail. |
| 5 | ScriptDetailView | `src/views/ScriptDetailView.vue` | Tabbed: Editor (KfScriptEditor), Test (KfScriptTester), History (KfHistoryViewer), Assignments |
| 6 | ShadowTestView | `src/views/ShadowTestView.vue` | Function selector → candidate editor → start/monitor shadow test → live results table → promote/discard |
| 7 | SettingsView | `src/views/SettingsView.vue` | Structured sections: Kafka, Processing, Script Reload, Connection Strings |
| 8 | MetricsView | `src/views/MetricsView.vue` | Real-time charts: throughput, latency, function breakdown, consumer lag |
| 9 | RulesView | `src/views/RulesView.vue` | Placeholder: "Coming soon" with explanation of rules feature |

### Step 4.5 — Components (App-Specific)

| # | Task | File | Details |
|---|------|------|---------|
| 1 | Sidebar | `src/components/AppSidebar.vue` | Navigation links, active state, connection badge |
| 2 | ConnectionBadge | `src/components/ConnectionBadge.vue` | Green/yellow/red dot based on SignalR hub states |
| 3 | FunctionTable | `src/components/FunctionTable.vue` | Reusable sortable/filterable table for functions |
| 4 | ScriptTable | `src/components/ScriptTable.vue` | Reusable table for scripts |
| 5 | ShadowResultsTable | `src/components/ShadowResultsTable.vue` | Live-updating table for shadow test results. Color-coded diffs. |
| 6 | MetricChart | `src/components/MetricChart.vue` | Simple SVG/Canvas line chart for real-time data |

### Step 4.6 — Build & Development Scripts

| # | Script | File | Details |
|---|--------|------|---------|
| 1 | Dev | `scripts/dev.ps1` | `npm run dev` |
| 2 | Build | `scripts/build.ps1` | `npm run build` → dist/ |
| 3 | Type check | In package.json | `vue-tsc -b` |

### Step 4.7 — Validation

| # | Task | Details |
|---|------|---------|
| 1 | Dev mode | Start KafkaProcessor backend + `npm run dev` → verify all views load |
| 2 | Script CRUD | Create, edit, save, history, rollback a script through the UI |
| 3 | Shadow test | Run a shadow test, verify SignalR results stream in real-time |
| 4 | Type check | `npm run typecheck` — no errors |
| 5 | Production build | `npm run build` → dist/ → verify static serving from ASP.NET backend |

---

## Phase 5: Integration into KafkaProcessor

**Existing Repo:** KafkaProcessor (under `apps/KafkaProcessor/`)
**Dependency:** All 4 phases complete

### Step 5.1 — Update KafkaProcessor.csproj

| # | Task | Details |
|---|------|---------|
| 1 | Add NuGet references | KoreForge.Scripts 0.0.1-alpha, KafkaProcessor.Scripts.API 0.0.1-alpha |
| 2 | Remove/deprecate | FieldExtractionRules loading path (keep for migration fallback) |

### Step 5.2 — Update Program.cs

| # | Task | Details |
|---|------|---------|
| 1 | Register KoreForge.Scripts | `builder.Services.AddKoreForgeScripts(...)` |
| 2 | Register KafkaProcessor.Scripts.API | `builder.Services.AddKafkaProcessorScriptsApi(...)` |
| 3 | Register JEX compiler | `builder.Services.AddScriptCompiler<JexScriptCompiler>("jex")` |
| 4 | Add SignalR | `builder.Services.AddSignalR()` |
| 5 | Add CORS | Dashboard policy for localhost:5173 |
| 6 | Map endpoints | `app.MapScriptEndpoints(); app.MapFunctionEndpoints(); app.MapShadowTestEndpoints()` |
| 7 | Map hubs | MetricsHub, SettingsHub, ShadowTestHub |
| 8 | Static files | `app.UseStaticFiles(); app.MapFallbackToFile("index.html")` |

### Step 5.3 — Database Migration

| # | Task | Details |
|---|------|---------|
| 1 | Add scripts to docker/sql/init.sql | CREATE TABLE dbo.Scripts, dbo.ScriptHistory, dbo.FunctionScripts |
| 2 | Seed initial scripts | Use JexScriptGenerator to produce .jex content from existing FieldExtractionRules → INSERT into Scripts table |
| 3 | Seed assignments | INSERT into FunctionScripts linking each function to its extract script |

### Step 5.4 — Update FunctionDefinitionLoader

| # | Task | Details |
|---|------|---------|
| 1 | Dual-path loading | Try Scripts table first (via FunctionScripts junction). Fall back to FieldExtractionRules + JexScriptGenerator. |
| 2 | Script reload subscription | Delegate to ScriptReloadService for hot-swap on change detection. |

### Step 5.5 — Update Pipeline ExtractFieldsStep

| # | Task | Details |
|---|------|---------|
| 1 | Shadow test hook | If ShadowTestService has active session for current functionId → run candidate script → emit result via hub |

### Step 5.6 — Update Existing Tests

| # | Task | Details |
|---|------|---------|
| 1 | Update 72 existing tests | Ensure they still pass with new script loading path |
| 2 | Add integration tests | End-to-end: upload script via API → verify it's loaded by pipeline → process message → correct output |

### Step 5.7 — Build Dashboard into Backend

| # | Task | Details |
|---|------|---------|
| 1 | Dockerfile multi-stage | Stage 1: `npm ci && npm run build` in KafkaProcessor.Dashboard. Stage 2: `dotnet publish`. Stage 3: copy both into runtime image. |
| 2 | Copy dist locally | For dev: symlink or copy KafkaProcessor.Dashboard/dist → KafkaProcessor/src/KafkaProcessor/wwwroot |

---

## Summary: Execution Order

```
Phase 1: KoreForge.Scripts         ─── NuGet ──→ nuget.org
   │
   ▼
Phase 2: KafkaProcessor.Scripts.API ─── NuGet ──→ nuget.org
   │
   │  (parallel)
   │
Phase 3: KoreForge.Vue.Scripts     ─── NPM ───→ npmjs.com
   │
   ▼
Phase 4: KafkaProcessor.Dashboard  ─── (app, not published)
   │
   ▼
Phase 5: Integration into KafkaProcessor
```

Phase 2 and Phase 3 can run in **parallel** — they have no dependency on each other. Phase 2 depends on Phase 1 (NuGet). Phase 4 depends on both Phase 2 (backend running) and Phase 3 (NPM package). Phase 5 depends on all prior phases.
