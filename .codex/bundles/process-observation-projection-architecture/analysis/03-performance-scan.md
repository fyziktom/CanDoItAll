# .NET Performance Scan

Skill used: `analyzing-dotnet-performance`

Scope scanned:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace*.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.Support.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeStateOverviewService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOperatorControlPlane.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOutbox.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs`

## Scan Execution Checklist

| Recipe | Hit Count | Interpretation |
| --- | ---: | --- |
| `.IndexOf("` missing comparison signal | 0 | No issue found in scanned scope. |
| `.Substring(` | 0 | No substring allocation signal in scanned scope. |
| `.StartsWith` / `.EndsWith` / `.Contains(` | 45 | Mostly collection predicates and explicit string comparisons; review any new hot-path string contains when implementing observation search. |
| `.ToLower()` / `.ToUpper()` | 0 | No culture/allocation signal in scanned scope. |
| `.Replace(` | 0 | No chained replace signal in scanned scope. |
| `params` | 0 | No params allocation signal in scanned scope. |
| LINQ `.Select` / `.Where` / `.OrderBy` / `.GroupBy` | 221 | Expected in read projections, but high enough that new live dashboard paths must be windowed and benchmarked. |
| `.Any` / `.All` | 17 | Acceptable now; avoid repeated nested scans in observation hot paths. |
| `new Dictionary<` / `new List<` | 14 | Normal DTO assembly today; avoid per-refresh excess allocation in high-volume snapshots. |
| `static readonly Dictionary<` | 0 | No FrozenDictionary candidate in this scoped scan. |
| `RegexOptions.Compiled` | 0 | No compiled regex budget issue. |
| `new Regex(` | 0 | No per-call regex issue. |
| `[GeneratedRegex]` | 0 | No regex path found. |
| public/internal non-sealed class signal | 5 | Most concrete service/read types are already sealed or partial component types; no broad structural performance issue. |
| sealed class signal | 19 | Positive evidence of sealed read/service helpers. |
| `.AsNoTracking()` | 33 | Positive evidence for read-model query paths. |
| `.ToList()` | 66 | Normal materialization in read projections; must stay bounded for live observation. |

## Findings For Planning

### Moderate Planning Risk: Read-projection LINQ and materialization become expensive under live multi-process refresh

Impact: current projection code is reasonable for selected-process views, but the same style can overload the app if a dashboard refreshes many processes and stages every few seconds.

Fix direction: introduce bounded observation queries, virtualized/windowed item providers, cache-aware snapshots, and coalesced refresh instead of direct page-level fan-out.

### Info: Existing string/regex anti-patterns are not the current bottleneck

Impact: the scanned hot observation paths showed no `Substring`, `ToLower`, `ToUpper`, chained `Replace`, or regex allocation signals.

Fix direction: keep this good baseline; require explicit `StringComparison` for future string filters and avoid culture-sensitive string normalization in live search.

### Positive Evidence

- `AsNoTracking` is already common in runtime read queries.
- Full selected-run details are separated into `ProcessWorkspaceRunDetailsLoader`.
- Active-run summaries already use a lightweight batch health metric path.
- Existing overview read model is immutable record-based, which is a good shape for the future observation projection.
