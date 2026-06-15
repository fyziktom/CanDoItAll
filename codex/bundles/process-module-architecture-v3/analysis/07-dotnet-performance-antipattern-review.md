# .NET Performance Antipattern Architecture Review

## Purpose

This review applies the `analyzing-dotnet-performance` skill to the new Process architecture bundle. The current task is architecture-only, so the scan uses the current Process implementation as risk evidence and then translates the findings into rewrite guardrails.

The result is not a request to optimize the old module. The old module remains reference material. The result is a set of required performance constraints for the new Process runtime, dispatcher, manager, projections, templates, drivers, adapters, and UI implementation subbundles.

## Scan Scope

Current implementation paths scanned as risk evidence:

- `src/CanDoItAll.Modules.Processes`
- `src/CanDoItAll.Processes.Contracts`
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.Drivers.*`

File types scanned:

- `.cs`
- `.razor`

Total scanned files: 528.

## Hot Path Context For The Rewrite

The new implementation must treat these as performance-sensitive paths:

- runtime state transitions,
- dispatcher claim/lease/heartbeat/result submission,
- event emission and outbox enqueue,
- projection workers and snapshot cache updates,
- live/history query handling,
- artifact ledger queries and recovery/resupply lookup,
- branch route evaluation,
- manager incident loop and recovery policy checks,
- subprocess parent/child message routing,
- template migration over many templates,
- Git status/diff operations over configuration trees,
- UI projection queries for definition lists, runs, activity, live dashboards, and canvases,
- external adapter calls to agents, workflows, browser proof, file systems, and HTTP services.

## Scan Execution Checklist

| Pattern checked | Count | Architectural interpretation |
| --- | ---: | --- |
| `.cs` / `.razor` files scanned | 528 | Large enough module surface that systematic patterns matter. |
| `async void` heuristic | 0 | Good current signal; preserve by forbidding `async void` except UI/event handlers. |
| `Task.Result` / `.Wait()` heuristic | 0 | Good current signal; preserve by forbidding sync-over-async in runtime, dispatcher, manager, projectors, adapters, and UI services. |
| `Task.Run` | 0 | Good current signal; preserve by forbidding library-level `Task.Run` wrappers. |
| `ValueTask` | 6 | Use only in measured hot paths or interface contracts where synchronous completion is frequent. |
| `.Substring(` | 6 | Rewrite parsing and routing helpers should prefer spans where called repeatedly. |
| `.IndexOf("literal")` without `StringComparison` heuristic | 0 | Preserve ordinal comparison discipline. |
| `.StartsWith` / `.EndsWith("literal")` without `StringComparison` heuristic | 0 | Preserve ordinal comparison discipline. |
| `.Contains("literal")` without `StringComparison` heuristic | 0 | Preserve ordinal comparison discipline for string checks. |
| `.ToLower()` / `.ToUpper()` empty overload | 0 | Preserve culture/ordinal discipline. |
| Chained `.Replace()` x3 single-line | 0 | Preserve by avoiding chained allocation-heavy string transforms in hot paths. |
| `params` signatures | 19 | Future hot-path helper APIs should avoid `params` array allocation or provide common overloads. |
| `RegexOptions.Compiled` | 2 | Prefer `[GeneratedRegex]` for static patterns and `NonBacktracking` for untrusted input. |
| `[GeneratedRegex]` | 4 | Positive current signal. |
| `new Regex(` | 0 | Preserve by avoiding per-call regex construction. |
| `static readonly Dictionary<` | 0 | No current static dictionary signal in scanned Process paths. |
| `static readonly FrozenDictionary<` | 0 | Future read-heavy lookup tables should use frozen collections where appropriate. |
| `new List<` | 168 | Strong allocation signal; future hot paths need pre-sizing, pooling only where justified, and streaming loops. |
| `new Dictionary<` | 112 | Strong allocation signal; future hot paths need pre-sizing, `TryGetValue`, and frozen/read-only lookup tables. |
| LINQ chain candidates | 2,128 | Highest allocation-risk signal; future projectors/runtime/canvas paths need explicit LINQ restrictions. |
| `new HttpClient(` | 0 | Good current signal; preserve with `IHttpClientFactory` or typed clients. |
| `new JsonSerializerOptions(` | 5 | Future template/runtime serializers must use cached options and source-generated contexts. |
| Unsealed class heuristic | 26 | Current code mostly seals classes; preserve by sealing leaf implementation classes. |
| `sealed class` | 303 | Positive current signal. |

## Findings

### Critical Architecture Risks

#### PERF-001. Sync-over-async must stay forbidden

**Impact:** A single `.Result` or `.Wait()` in dispatcher, manager, projector, or adapter code can cause thread pool starvation and stalled process execution.

**Evidence:** Current scan found 0 `Task.Result` / `.Wait()` heuristic matches, which is a good baseline to preserve.

**Architecture fix:** All runtime, dispatcher, manager, persistence, projection, Git, template, adapter, and UI service APIs that perform I/O must be async end-to-end and cancellation-aware. Synchronous wrappers over async APIs are forbidden.

#### PERF-002. Runtime event monitoring must use bounded non-blocking pipelines

**Impact:** Unbounded observers or blocking projection callbacks can make monitoring slow down process execution, directly violating the Process-as-operating-system design.

**Evidence:** The v3 architecture already separates monitoring through events and snapshots, but it did not explicitly require bounded channels/backpressure and drop/dead-letter behavior for projection lag.

**Architecture fix:** Runtime event publication must persist the event/outbox record first, then notify observers through bounded `Channel<T>` or equivalent queue contracts. Projector lag must be observable. Full queues must apply an explicit policy: wait with cancellation, shed non-critical projection work, or dead-letter; never block runtime state transitions indefinitely.

### Moderate Architecture Risks

#### PERF-003. LINQ-heavy projection and canvas code is a predictable allocation trap

**Impact:** LINQ is acceptable in cold paths, but event projectors, live snapshots, runtime canvas builders, and large definition/run lists can allocate heavily and repeatedly.

**Evidence:** Current Process paths contain 2,128 LINQ chain candidates, many around canvas and projection-like composition code.

**Architecture fix:** Hot-path projectors, runtime readers, canvas projection builders, and dashboard query handlers must use explicit loops, pre-sized collections, and single-pass transformations unless profiling proves LINQ cost is irrelevant. Cold-path template/admin code may use LINQ when clarity is better and data sets are bounded.

#### PERF-004. Per-call collection allocation must be controlled

**Impact:** Repeated `new List<>` and `new Dictionary<>` in projection and runtime loops creates avoidable GC pressure under concurrent process runs.

**Evidence:** Current Process paths contain 168 `new List<>` and 112 `new Dictionary<>` matches.

**Architecture fix:** Projection and runtime builders must pre-size collections from known counts, use `TryGetNonEnumeratedCount` for unknown enumerables, avoid multiple enumeration, and use `FrozenDictionary` / `FrozenSet` for read-heavy lookup tables created once. Do not use pooling unless ownership and clearing rules are simple and tested.

#### PERF-005. JSON template/runtime serialization must be source-generated and option-stable

**Impact:** Recreating `JsonSerializerOptions` and using reflection-based serialization during template migration, event replay, or snapshot projection can become a major startup and migration cost.

**Evidence:** Current Process paths contain 5 `new JsonSerializerOptions(...)` matches.

**Architecture fix:** New Process projects must define source-generated `JsonSerializerContext` types for template source, migration reports, exchange envelopes, runtime events, snapshots, artifact ledgers, and Git metadata. `JsonSerializerOptions` must be static cached options or generated context defaults. Large migration reads should stream or batch rather than load all documents into one object graph.

#### PERF-006. Regex usage must be generated, bounded, or avoided

**Impact:** Regex in branch parsing, artifact mapping, template migration, or diagnostics can add startup cost or catastrophic backtracking risk.

**Evidence:** Current Process paths contain 2 `RegexOptions.Compiled` and 4 `[GeneratedRegex]` matches.

**Architecture fix:** Static regex patterns must use `[GeneratedRegex]`. Untrusted or user-supplied pattern inputs must use `RegexOptions.NonBacktracking` where regex is allowed at all. Prefer typed parsing over regex for branch routes and process identifiers.

#### PERF-007. UI projection queries must be paged and virtualized

**Impact:** Loading all definitions, all runs, all events, all artifacts, or all live history into Blazor components will recreate the current slow Process experience under a cleaner backend.

**Evidence:** Current UI exposes definition catalogs, run history, live activity, template catalog, graph/analytics, and evidence lists. The user specifically observed incorrect live-hour behavior with old events showing in the live view.

**Architecture fix:** UI query contracts must include server-side filtering, paging/windowing, continuation tokens where needed, cancellation, projection freshness, and stable sort keys. Blazor components must render bounded result sets and request more data explicitly.

#### PERF-008. Template migration must be bounded and resumable

**Impact:** Migrating 1000 processes/templates by loading everything into memory or running unbounded parallel file/JSON/Git operations can create high GC pressure, I/O contention, and partial migration failure.

**Evidence:** The v3 architecture intentionally supports migrating all templates, but it did not explicitly require chunking, bounded parallelism, and checkpointed progress.

**Architecture fix:** Template migration must process documents in bounded batches, use async file I/O, cache serializer contexts/options, checkpoint completed migrations, and support resume after failure. Parallelism must be explicitly limited.

#### PERF-009. External adapters must not create per-call clients or buffer large responses by default

**Impact:** Agent, workflow, browser proof, Git remote, and HTTP integrations can exhaust sockets or memory if each execution creates clients or buffers full responses.

**Evidence:** Current Process scan found 0 `new HttpClient(` matches, a good baseline.

**Architecture fix:** External HTTP adapters must use `IHttpClientFactory` or typed clients. Large downloads/uploads must use `HttpCompletionOption.ResponseHeadersRead`, stream APIs, and cancellation-aware copy loops.

#### PERF-010. Leaf implementation classes should stay sealed unless extension is required

**Impact:** Unsealed leaf classes reduce JIT devirtualization and make large service-heavy code harder to reason about.

**Evidence:** Current Process paths show 303 sealed classes and 26 unsealed class heuristic matches, a strong positive baseline.

**Architecture fix:** Future implementation classes in runtime, dispatcher, manager, persistence, projectors, adapters, and UI presenters should be sealed by default. Keep interfaces for real boundaries and tests, not for trivial single-implementation abstractions.

## Architecture Adjustments Required

- Add a dedicated performance guardrail architecture file for runtime, dispatcher, persistence, projectors, templates, drivers, adapters, and UI.
- Add performance scan and review gates to future implementation and QA prompts.
- Add a hardening gate requiring exact scan counts, not estimates.
- Add stop conditions for sync-over-async, unbounded queues, uncached serializer options, per-call clients, UI load-all queries, and old dispatcher fallback.
- Require each future subbundle touching hot-path C# code to report performance scan counts and any accepted tradeoffs.

## Disclaimer

These results are generated by an AI assistant and are non-deterministic. Findings may include false positives, miss real issues, or suggest changes that are incorrect for this codebase. Future implementation agents must verify recommendations with tests, profiling, and human review before optimizing production code beyond the architecture guardrails.
