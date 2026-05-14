# Re-entry Architecture And Performance Review

## Date

- 2026-05-10

## Gate Result

- Implemented app path remains acceptable for preview/test/non-durable workflow use.
- Two architecture repairs were applied during re-entry review:
  - MAF checkpoint implementation moved from `CanDoItAll.AgentFramework.Core` to `CanDoItAll.AgentFramework.Maf`.
  - Workflow catalog saves now snapshot incoming workflow graphs before storing them.
- Production durable runtime, generated Azure Functions endpoint governance, and persistent workflow product storage remain open production blockers.

## Architecture Findings

### Fixed: MAF helper leaked into Core

- Evidence: `CanDoItAll.AgentFramework.Core.csproj` directly referenced `Microsoft.Agents.AI.Workflows`, and `ExecutionCheckpointServices.cs` used `FileSystemJsonCheckpointStore`.
- Risk: Core was no longer provider/runtime-neutral; any consumer of Core pulled MAF workflow implementation details into the application-service boundary.
- Fix: `WorkflowBackedAgentExecutionCheckpointBridge` now lives in `CanDoItAll.AgentFramework.Maf`; Core retains only `IAgentExecutionCheckpointBridge` and `NullAgentExecutionCheckpointBridge`.
- Guard: `WorkflowArchitectureBoundaryTests.AgentFrameworkCoreDoesNotReferenceMafWorkflowPackage`.

### Fixed: catalog stored mutable caller graph references

- Evidence: `InMemoryWorkflowCatalogService.SaveDefinitionAsync` stored `request.Graph` directly.
- Risk: a caller passing mutable lists could mutate the saved canonical workflow definition after save.
- Fix: the catalog now snapshots graph nodes, ports, and edges at the save boundary.
- Guard: `WorkflowCatalogTests.CatalogSnapshotsDefinitionGraphOnSave`.

### Open: runtime semantics are still preview-level

- Evidence: `MafWorkflowCompiler.CreateExecutorBinding` maps every workflow node to a generic executor that returns the input payload.
- Risk: LLM calls, triage, strict logic, artifacts, agent steps, and subworkflow steps are modeled and authorable but not yet executed with production semantics.
- Required next action: add node-kind-specific execution handlers behind product workflow contracts before treating workflow test runs as semantic proof.

### Open: human-input routing is coarse

- Evidence: `WorkflowRuntimeManager.StartAsync` pauses on the first human-input node found in the graph before backend execution.
- Risk: a human-input node later in the graph can make the run wait immediately, regardless of graph reachability or upstream execution.
- Required next action: let the workflow backend surface product external requests from actual execution position, or explicitly restrict preview human-input semantics in validation.

## Performance Scan Checklist

Scope: workflow runtime/catalog/model/API/UI files plus process workflow coordinator and the MAF checkpoint bridge.

| Recipe | Count | Result |
| --- | ---: | --- |
| `.IndexOf("...")` without `StringComparison` | 0 | Passed |
| `.Substring(` | 0 | Passed |
| `.StartsWith`/`.EndsWith` literal without `StringComparison` | 0 | Passed |
| `.Contains("...")` candidate without `StringComparison` | 0 | Passed |
| `.ToLower()`/`.ToUpper()` | 0 | Passed |
| Chained `.Replace()` 3+ | 0 | Passed |
| `params` signatures | 0 | Passed |
| LINQ `Select`/`Where`/`Cast`/`Take`/`Aggregate` | 55 | Accepted; projection/validation paths, not tight polling loops |
| `.All`/`.Any` char predicate candidate | 0 | Passed |
| `static readonly Dictionary<` | 0 | Passed |
| `static readonly FrozenDictionary<` | 0 | No static dictionary candidates |
| `new List<` | 5 | Accepted; local result building only |
| `new Dictionary<` | 2 | Accepted; static readonly UI metadata in component mapper |
| `StringComparer.CurrentCulture` | 0 | Passed |
| `new HttpClient(` | 0 | Passed |
| uncached `new JsonSerializerOptions` | 0 | Passed |
| `JsonSerializer.Serialize/Deserialize` | 4 | Accepted; cached options or explicit API/projection serialization |
| Regex usage | 0 | Passed |
| `Task.Run(` | 0 | Passed |
| `Thread.Sleep(` | 0 | Passed |
| potential sync wait `.Result`/`.Wait` | 0 | Passed |
| `async void` | 0 | Passed |
| `ValueTask` usage | 3 | Accepted; local MAF executor binding synchronous completion |
| structural declarations | 70 total; 64 sealed/record; 2 unsealed Razor partial components | Passed |

## Proof

- `dotnet build src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:OutDir=.codex\tmp\workflow-core-isolation-build\` passed.
- `dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:OutDir=.codex\tmp\workflow-maf-isolation-build-2\` passed.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter FullyQualifiedName~WorkflowArchitectureBoundaryTests --artifacts-path .codex\tmp\artifacts\workflow-boundary -p:UseSharedCompilation=false` passed 1/1.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter FullyQualifiedName~WorkflowFoundationTests --artifacts-path .codex\tmp\artifacts\workflow-foundation -p:UseSharedCompilation=false` passed 10/10.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter FullyQualifiedName~WorkflowCatalogTests --artifacts-path .codex\tmp\artifacts\workflow-catalog -p:UseSharedCompilation=false` passed 10/10.
- `dotnet build tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --artifacts-path .codex\tmp\artifacts\workflow-integration-build -m:1 -p:UseSharedCompilation=false` passed with 0 warnings/errors.

## Validation Notes

- Default `obj` outputs are locked by the running app, so validation used `--artifacts-path` or isolated `OutDir` paths.
- A failed attempt to place `BaseIntermediateOutputPath` under per-project `.codex\tmp` generated duplicate assembly attributes because SDK-generated `.cs` files were included by default globs; those generated `workflow-boundary-obj` folders were removed.
