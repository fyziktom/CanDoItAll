# Performance And EF Scan

## Scan Scope

The targeted scan covered plugin runtime, concrete plugin projects, workflow executor invocation/observability, workflow canvas executor catalog, composition, and relevant tests.

Primary paths:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins`
- `C:\repositories\CanDoItAll\src\plugins`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition`
- `C:\repositories\CanDoItAll\tests`

## Targeted Pattern Counts

- `.IndexOf("` candidates: 1
- `StartsWith`/`EndsWith`/`Contains` candidates: 71
- `ToLower`/`ToUpper`: 0
- `Substring`: 0
- `Replace`: 24
- LINQ query operators: 199
- Per-call `Dictionary`/`List` allocations: 41
- Static readonly dictionary candidates: 0
- `RegexOptions.Compiled`: 6
- `new Regex`: 0
- `GeneratedRegex`: 0
- EF `AsNoTracking`: 13
- EF `ToArray`/`ToList` materialization candidates: 98
- EF `Include`: 0
- Count-existence candidates: 6, all duplicate-group or in-memory UI counts in the inspected scope
- Sync DbContext creation: 2
- Sync `SingleOrDefault`: 10
- Public/internal classes: 28
- Sealed classes: 77

## Findings

### PERF-EF-001: In-memory latest connection selection

`PluginConnectionStore.FindFirstByKeyAsync` materializes all matching connections and then orders in memory:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPermissionServices.cs:146`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPermissionServices.cs:155`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPermissionServices.cs:157`

Required improvement: push ordering and first-row selection into the database, then project to `PluginConnectionItem`.

### PERF-EF-002: OAuth workflow connection resolution materializes candidates before latest selection

`PluginOAuthService.ResolveWorkflowConnectionIdAsync` materializes joined connection/OAuth candidates before ordering:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\OAuth\PluginOAuthService.cs:329`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\OAuth\PluginOAuthService.cs:364`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\OAuth\PluginOAuthService.cs:365`

Required improvement: keep ordering and candidate narrowing in EF where possible. If scope JSON filtering cannot be translated, reduce and order the query before materialization and document the bounded in-memory step.

### PERF-EF-003: Executor descriptor availability can cause repeated sync DB reads

Concrete executor `Descriptor` properties call `ResolveAvailability`, which calls grant evaluation. The grant evaluator has sync paths backed by sync DbContext creation/listing:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs:55`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPermissionServices.cs:32`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPermissionServices.cs:278`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowExecutors.cs:34`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowExecutors.cs:148`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs:22`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs:163`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs:21`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs:162`

Required improvement: avoid per-descriptor sync database reads while building executor catalogs. Prefer a scoped async/batch availability snapshot or an explicit cached grants snapshot passed into catalog construction.

### PERF-IO-004: Recursive installed manifest scans are a correctness and performance risk

Installed manifests are recursively enumerated:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:299`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:787`

Required improvement: enumerate direct package roots only and validate the root path/package identity relationship.

## Positive Findings

- Read-only plugin EF queries generally use `AsNoTracking`.
- No broad `ToLower`/`ToUpper` culture/allocation issue was found in the targeted plugin scope.
- No `new Regex(...)` hot path was found; observed regex usage relies on compiled static patterns.
- No EF `Include` over-fetch pattern was found in the targeted plugin scope.

## Closure Expectations

Subbundle 05 must rerun targeted searches after implementation and update this file or the execution report with resolved findings. It must prove query behavior with tests or a clear inspection note for every finding above.
