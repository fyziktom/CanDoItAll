# SB01 Performance Review and Deep Pattern Scan

## Scope

Target framework: .NET 10.

Hot path: agent acceptance through catalog/provider/session/run creation, MAF composition, first runtime feedback, process-manager bootstrap, and project/process snapshot assembly.

Scanned:

- Agent Framework Core execution/context/reference data;
- file persistence storage;
- MAF runtime;
- Agent Framework module services/workspace/providers/chat panel;
- Process Manager shell/context and process projection application services;
- Project Structure context/tool/snapshot services.

## Pass 1: Initial Performance Review

### Root cause

The dominant latency is architectural I/O and sequencing, not a micro-allocation: before the first backend status, the path performs at least two catalog reads, provider EF lookup, one or two session reads, active-run lookup, initial run/transcript write, attachment/handoff/credential work, and a second run-detail read/write. Every later feedback entry also persists before notification. Process Manager additionally bypasses the common live feedback path, and Project Structure tools can rebuild the full multi-source projection despite the page already holding it.

### Recommended changes

1. Publish typed operational activity before canonical persistence.
2. Pass one prepared startup aggregate through run creation to eliminate duplicate catalog/session reads.
3. Reuse immutable held module snapshots for prompt/tool reads.
4. Build a bounded revisioned per-agent blueprint, never a live-runtime pool.
5. Overlap only provider/session reads after the provider no longer falls back to the same file store.
6. Preserve sequential runtime capability/tool composition because it mutates ordered state.
7. Correlate existing composition/provider metrics and add acceptance/run-binding/runtime-entry timestamps.

### Expected impact

- Time to first truthful backend activity becomes effectively immediate and independent of store locks.
- Warm startup removes duplicate file I/O and avoids full project/process reassembly.
- Runtime composition remains correct; its remaining cost becomes attributable rather than hidden.

### Trade-offs

- Bounded replay/eviction and snapshot revisions add explicit lifecycle code.
- Exact wall-clock gain depends on cold/warm store state, provider DB latency, file lock contention, and enabled capabilities.

## Pass 2: Deep Pattern Scan

### Scan execution checklist

| Recipe | Hits |
| --- | ---: |
| Async/task signal | 4,068 |
| Memory/string signal | 116 |
| Regex signal | 17 |
| Collection/LINQ signal | 2,726 |
| I/O/serialization signal | 241 |
| `IndexOf` literal without comparison | 0 |
| `Substring` | 1 |
| literal `StartsWith`/`EndsWith` without comparison | 0 |
| literal `Contains` without comparison | 1 |
| `async void` | 0 |
| static `Dictionary` / `FrozenDictionary` | 0 / 0 |
| `new List` / `new Dictionary` candidates | 146 / 90 |
| `StringComparer.CurrentCulture` | 0 |
| hot-path LINQ candidates | 932 |
| `new HttpClient` | 0 |
| `new JsonSerializerOptions` | 3 |
| parameterless `ToLower`/`ToUpper` | 0 |
| triple `Replace` chain | 0 |
| `params` signatures | 17 |
| LINQ `All`/`Any` over `char` | 1 |
| sync-over-async textual candidates | 5 |
| regex compiled / generated / uncached `new Regex` | 0 / 4 / 0 |
| unsealed / sealed class declarations | 2 / 219 |

### Classification

No new critical API anti-pattern was confirmed after manual review:

- all five sync-over-async textual candidates were domain properties such as `ResultHash`/`ResultSummary`;
- the literal `Contains` hit is a collection lookup, not a string comparison;
- all three `JsonSerializerOptions` instances initialize static cached fields;
- all four static regexes use `[GeneratedRegex]`; no compiled/uncached regex exists;
- `Substring` builds the returned search snippet, so replacing it with a span alone would not remove the required result allocation;
- the two-character LINQ check and unsealed exception/component are outside the measured startup bottleneck;
- list/dictionary/LINQ candidates mostly build immutable/result projections and require measurement, not mechanical rewriting.

### Findings

#### 1. Hidden architectural I/O dominates (1 critical path)

**Impact:** duplicated reads plus persist-before-notify create the visible freeze and lock-quantized latency.

**Files:** `AgentFrameworkWorkspaceExecutionService.Chat.cs`, `AgentFrameworkWorkspaceExecutionService.Helpers.cs`, `FileSandboxWorkspaceStore.cs`

**Fix:** publish pre-run activity, carry one prepared aggregate, and measure read/write counts.

#### 2. Progress notification is coupled to storage and subscribers (2 relays)

**Impact:** slow storage delays every update; a throwing synchronous subscriber can make persisted success look failed.

**Files:** `AgentFrameworkWorkspaceExecutionService.Helpers.cs`, `CurrentProfileAgentFrameworkWorkspaceService.cs`

**Fix:** use the typed sequenced projection and isolate compatibility subscribers.

#### 3. Shared-load cancellation ownership is incorrect (1 cache path)

**Impact:** the first caller can cancel all waiters while later waiter cancellation is ignored.

**Files:** `WorkspaceBackedAgentReferenceDataProvider.cs`, `AgentReferenceDataCache.cs`

**Fix:** service-owned factory token plus per-waiter `WaitAsync`.

#### 4. Runtime/project metrics are insufficient (2 measurement boundaries)

**Impact:** production discards capability timing and cannot attribute catalog/provider/session/run/provider-first-update delay.

**Files:** `MafRuntimeServiceCollectionExtensions.cs`, `MafRuntimeContracts.cs`, `MafAgentRuntime.cs`

**Fix:** bounded correlated measurements and deterministic operation-count probes.

### Positive findings

- ✅ Read-only provider/settings EF queries use `AsNoTracking` and factory-created contexts.
- ✅ Process persistence read queries inspected use `AsNoTracking`, explicit `Take`, and projections/bounded result shapes.
- ✅ No `async void`, confirmed sync-over-async, uncached `HttpClient`, culture-sensitive hot comparison, uncached JSON options, or uncached regex was found in scope.
- ✅ 219 of 221 scanned class declarations are sealed; the two exceptions are not the measured bottleneck.
- ✅ Process runtime reads reuse `PreviouslyLoadedRuns`; startup work should extend that reuse rather than parallelize one scoped process `DbContext`.

| Severity | Count | Top issue |
| --- | ---: | --- |
| 🔴 Critical | 1 | Duplicated/persist-gated pre-first-feedback path |
| 🟡 Moderate | 3 | Subscriber coupling, cancellation ownership, missing correlated metrics |
| ℹ️ Info | 0 | None recommended for change without profiling |

> ⚠️ **Disclaimer:** These results are generated by an AI assistant and are non-deterministic. Findings may include false positives, miss real issues, or suggest changes that are incorrect for your specific context. Always verify recommendations with benchmarks and human review before applying changes to production code.
