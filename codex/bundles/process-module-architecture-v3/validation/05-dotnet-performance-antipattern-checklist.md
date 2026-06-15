# .NET Performance Antipattern Validation Checklist

## Purpose

Future Process implementation agents must use this checklist when a subbundle creates or modifies C#/.NET code in runtime, dispatcher, manager, persistence, projections, templates, Git wrapper, drivers, adapters, candidate readiness evaluation, or UI services.

This checklist is based on `analysis/07-dotnet-performance-antipattern-review.md` and `architecture/19-dotnet-performance-guardrails.md`.

## Required Scan Scope

For each implementation subbundle, scan:

- new or modified `.cs` files,
- new or modified `.razor` / `.razor.cs` files that contain non-trivial logic,
- generated helper code only if it is committed and maintained by the project,
- tests only when the subbundle introduces reusable test helpers or performance-sensitive fixtures.

## Scan Execution Checklist

Use `rg` or an equivalent command and record exact counts:

| Pattern | Required check |
| --- | --- |
| Sync-over-async | `.Result`, `.Wait()`, `GetAwaiter().GetResult()` in new/modified code. |
| Fake async | `Task.Run(` in library/runtime/service code. |
| Fire-and-forget | `_ = SomeAsync(...)`, `Task.Factory.StartNew`, manually created background tasks. |
| `async void` | Allowed only for UI/event handlers. |
| ValueTask | Confirm each use has frequent synchronous completion or required framework contract. |
| String slicing | `.Substring(` in parsing/routing/template hot paths. |
| String comparison | `.IndexOf`, `.Contains`, `.StartsWith`, `.EndsWith` without explicit ordinal comparison where non-linguistic. |
| Case conversion | `.ToLower()` / `.ToUpper()` empty overloads. |
| Regex | `RegexOptions.Compiled`, `new Regex(`, `[GeneratedRegex]`, and use of `NonBacktracking` for untrusted input. |
| Collection allocation | `new List<`, `new Dictionary<`, repeated per-call lookup maps. |
| LINQ hot paths | `.Select`, `.Where`, `.OrderBy`, `.GroupBy`, `.Aggregate`, `.ToList`, `.ToDictionary` inside runtime/projector/UI query hot paths. |
| Double lookup | `ContainsKey` followed by indexer. |
| Static lookup tables | `static readonly Dictionary<` candidates for `FrozenDictionary` / `FrozenSet`. |
| JSON options | `new JsonSerializerOptions` outside static cached fields or generated context setup. |
| Serialization context | `JsonSerializer.Serialize/Deserialize` without generated context where types are known. |
| HTTP clients | `new HttpClient(` outside typed client/factory setup. |
| File I/O | sync file APIs in async runtime/template/Git/projection paths. |
| Structural | unsealed leaf implementation classes. |

## Required Report Block

Every subbundle touching C# hot-path code must include this block in its execution report:

```text
Performance scan:
- Files scanned:
- Critical findings:
- Moderate findings:
- Info findings:
- Zero-count confirmations:
- Accepted tradeoffs:
- Benchmark/profiling evidence required later:
```

## Subbundle-Specific Hot Path Notes

| Subbundle range | Highest risk | Required additional proof |
| --- | --- | --- |
| SB03-SB06 | Core/builder abstractions can overuse strings, dictionaries, and reflection. | Strongly typed IDs, ordinal comparers, frozen lookup tables for static registries. |
| SB07-SB08 | Runtime/dispatcher/persistence can block, allocate per transition, or create unbounded queues. | Async end-to-end scan, bounded channel proof, event/outbox serialization scan. |
| SB09 | Manager/recovery can loop or allocate large diagnostic objects. | Bounded recovery proof, sanitized incident projection, no recursive unbounded retry. |
| SB10 | Projectors/live snapshots can use LINQ-heavy full rebuilds. | Incremental projector tests, time-window query tests, collection allocation review. |
| SB11 | Adapters can create clients per call or buffer large responses. | `IHttpClientFactory`/typed client proof, streaming/cancellation proof. |
| SB12 | Template migration can load everything into memory. | Bounded batch migration proof, source-generated JSON proof, checkpoint/resume proof. |
| SB13-SB20 | UI authoring and template screens can load full catalogs or compute projection truth in components. | Paging/filtering proof, component dependency scan, Playwright proof with bounded data. |
| SB21-SB25 | Launch/runtime/operator/evidence screens can recompute run truth or artifact state in UI; candidate readiness can fan out into repeated directory, tool-provider, rights, workflow, provider-profile, and assignment lookups. | Projection-only proof, no UI runtime query proof, cancellation-aware refresh, shared evidence loading, and no per-candidate external/provider calls when evidence can be batched. |
| SB26 | Live dashboard can mix windows or reload all history. | Live 1h/1d/7d/30d query-boundary tests and snapshot cache proof. |
| SB27 | Agent/API/project integration can bypass shared contracts or create per-call clients. | Tool/API contract tests and adapter performance scan. |
| SB28 | Final closure can miss systematic hot-path patterns. | Complete performance scan summary across all new Process projects. |

## Stop Conditions

Stop and report before handoff if any of the following is found in new production code:

- sync-over-async in runtime, dispatcher, manager, projector, adapter, persistence, or UI service paths,
- unbounded event/projector queues without written proof of natural producer limits,
- per-call `HttpClient`,
- per-call `JsonSerializerOptions`,
- template migration loading all templates into a single in-memory graph,
- live/history UI loading all historical events and filtering in the browser,
- generic core/runtime branch routing based on regex/free-text token parsing,
- candidate readiness evaluation performing repeated per-candidate external/provider calls where shared evidence can be loaded once,
- hot-path LINQ/collection allocations with no bounded data guarantee and no mitigation.
