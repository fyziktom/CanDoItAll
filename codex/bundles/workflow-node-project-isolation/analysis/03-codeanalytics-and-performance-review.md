# CodeAnalytics And Performance Review

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260629143729-e43d210b`
- Scope: workflow, MAF, plugin, process-adjacent, Workbench, Web, persistence, and selected process projects.
- Snapshot size: 20 source projects and 587 source documents.
- Blocking diagnostics: none.

## Pass 1: Initial Performance Review

The performance risk is architectural, not one isolated method. Workflow startup, executor catalog construction, template loading, preview simulation, plugin catalog projection, and workflow node startup can become hot paths when workflows are rendered in UI, previewed repeatedly, or executed through project-structure/process loops.

Recommended architectural performance constraints:

- Build descriptor catalogs once per appropriate DI scope and avoid repeated reflection over executor settings for every request.
- Cache immutable template pack materialization where filesystem contents and template version are stable.
- Keep `JsonSerializerOptions` and generated regex/static regex helpers centralized in executor/helper projects.
- Avoid LINQ-heavy code in execution loops, descriptor projection loops, and template graph conversion loops when profiles show repeated calls.
- Keep plugin grant evaluation explicit and observable, but avoid recomputing source/trust mapping multiple times per descriptor when catalog rows are generated.
- Prefer typed result and error models over repeated JSON parsing/re-parsing between templates, preview, runtime, and UI.

Expected impact: the main win is reduced allocation and less repeated catalog/template work during UI and workflow preview paths. Do not trade correctness or diagnostics for micro-optimizations.

## Pass 2: Deep Pattern Scan

Scoped paths:

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows`
- `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- `repo://src/CanDoItAll.Modules.Plugins`
- `repo://src/plugins`
- project-structure workflow node files in `repo://src/CanDoItAll.Modules.Workbench`

## Scan Execution Checklist

| Recipe | Hits | Planning decision |
| --- | ---: | --- |
| `.IndexOf(string)` without `StringComparison` | 0 | No action. |
| `.Substring(` allocations | 0 | No action. |
| `StartsWith`/`EndsWith` without `StringComparison` | 0 | No action. |
| `Contains(string)` without `StringComparison` heuristic | 0 | No action. |
| `async void` methods | 0 | No action. |
| `static readonly Dictionary<` | 0 | No frozen dictionary action. |
| `static readonly FrozenDictionary<` | 0 | No inverse issue. |
| `new List<` heuristic | 26 | Review only in hot template/runtime loops during hardening checkpoints. |
| `new Dictionary<` heuristic | 20 | Review repeated descriptor/template/plugin loops; many are legitimate request-local maps. |
| `StringComparer.CurrentCulture` | 0 | No action. |
| LINQ chains `.Select/.Where/.Cast/.Take/.Aggregate` | 251 | Treat as checkpoint review item, not blanket rewrite. |
| `.ToLower()`/`.ToUpper()` without culture | 0 | No action. |
| Chained `.Replace` 3+ | 0 | No action. |
| `params` signatures | 4 | Review only if call sites are hot. |
| LINQ char `All`/`Any` | 2 | Review Docker/Office365 validation paths if profiling shows hot-path use. |
| `RegexOptions.Compiled` | 6 | Prefer `[GeneratedRegex]` for static patterns during executor/helper extraction unless `NonBacktracking` or dynamic behavior is needed. |
| `[GeneratedRegex]` | 1 | Existing positive pattern in markdown executor. |
| `new Regex(` | 0 | No uncached construction found. |
| `new HttpClient(` | 0 | No socket exhaustion pattern found. |
| `new JsonSerializerOptions` | 14 | Consolidate/cached options in workflow/executor helper projects where safe. |
| Unsealed class heuristic | 2 unsealed partial Blazor page classes, 125 sealed classes | No action for partial component pages; keep new service classes sealed by default. |

## Findings For Bundle Planning

### High: Failure Diagnostics Need A Typed Contract

Impact: workflow/project isolation creates more seams where exception context can be lost. Current code has redaction and specific exception types, but user-facing failure display can still infer root cause by parsing exception text. Plugin and external tool/MCP failures need structured context to remain repairable.

Evidence:

- `WorkflowExecutorInvoker` wraps final failures in `WorkflowExecutorInvocationException` after sanitizing exceptions and recording audit messages.
- `WorkflowFailureDisplayFormatter` extracts executor/node/root cause from exception message text.
- `RuntimePackageWorkflowExecutor` delegates plugin execution directly to the inner executor, so package/plugin/type context must be preserved by the new adapter contract.
- `WorkflowEventRecord` has a stable `Message` plus `PayloadJson`, making typed payload compatibility the right place for detailed diagnostics.

Bundle action: R17, `architecture/04-failure-diagnostics-and-error-state-boundary.md`, and `inventories/06-error-state-inventory.md` now define the required diagnostic envelope, error kinds, owners, and tests.

### Moderate: Large Core/MAF Workflow Files

Impact: large files hide multiple responsibilities and make extraction hard to prove.

Files:

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/ProjectStructureWorkflowExecutor.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/SourceIngestionWorkflowExecutor.cs`

Bundle action: SB05, SB09, and SB13 must include file-size, responsibility, and dependency reviews before downstream adoption.

Updated planning decision: SB07, SB10, and SB11 now require split-by-responsibility work for the largest moved classes. A whole-file move is not acceptable proof of maintainability when the class already mixes settings parsing, IO/provider calls, policy enforcement, result shaping, and diagnostics.

### Moderate: Repeated Template/Catalog Allocation Candidates

Impact: template load, executor catalog projection, and UI preview paths can allocate excessively when used repeatedly.

Evidence: 251 LINQ-chain hits, 26 `new List<` hits, and 20 `new Dictionary<` hits in scoped paths.

Bundle action: do not blanket-rewrite. Require focused scans and benchmarks or representative timing at hardening checkpoints around template loading, descriptor catalog construction, and plugin projection.

### Moderate: Serializer And Regex Helper Duplication

Impact: repeated serializer option construction and compiled regex fields can create avoidable startup/allocation cost and AOT/trimming friction.

Evidence: 14 `new JsonSerializerOptions` hits and 6 `RegexOptions.Compiled` hits in scoped paths.

Bundle action: executor shared helpers should own serializer options and generated regex helpers where static patterns are compile-time constants.

## Disclaimer

These results are generated by an AI assistant and may include false positives or miss real issues. Execution must verify recommendations with tests, targeted benchmarks, or representative profiling before changing hot-path code.
