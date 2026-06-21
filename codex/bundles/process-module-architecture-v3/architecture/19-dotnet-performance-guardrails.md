# .NET Performance Guardrails

## Design Intent

The new Process module is an operating-system-like runtime. Performance mistakes in the runtime, dispatcher, manager, projection, template migration, and UI query layers will become reliability problems, not merely slow screens. The architecture must therefore prevent common .NET hot-path antipatterns before implementation begins.

This file complements, but does not replace:

- `architecture/05-runtime-dispatcher-and-state-machines.md`
- `architecture/08-monitoring-events-snapshots-and-ui-projections.md`
- `architecture/09-template-git-versioning-and-migrations.md`
- `architecture/12-runtime-persistence-event-store-and-outbox.md`
- `architecture/14-manager-runtime-and-control-loop.md`
- `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`
- `architecture/16-execution-adapters-and-integration-boundaries.md`
- `architecture/20-role-candidate-selection-and-readiness.md`

## Hot Path Classification

| Hot path | Required performance posture |
| --- | --- |
| Runtime transition application | Async end-to-end, no blocking, small allocations, explicit state machine, event persisted once. |
| Dispatcher claim/lease loop | Bounded polling/notifications, no sync-over-async, no per-iteration LINQ allocation, cancellation-aware waits. |
| Strategy execution dispatch | No runtime strategy rediscovery, no reflection scanning per step, cached strategy descriptors. |
| Event/outbox write | Durable write first, compact envelope, source-generated serialization, idempotency keys. |
| Projection workers | Bounded channels, batched reads/writes, explicit offsets, no runtime blocking, no unbounded memory queues. |
| Live snapshot cache | Incremental updates, fixed-size/current indexes, query-window enforcement, freshness metadata. |
| Artifact ledger lookup | Indexed queries, typed references, `TryGetValue` patterns in memory, no repeated scans of all artifacts. |
| Branch route evaluation | Typed route tables and precompiled lookup maps, no regex/string-token routing in hot path. |
| Manager recovery loop | Bounded attempts, bounded concurrency, idempotent commands, no recursive unbounded retries. |
| Candidate readiness evaluation | Compile requirements once per launch role, load shared evidence snapshots in batches, evaluate candidates in memory against typed evidence, no per-candidate provider fan-out when shared evidence can be queried once. |
| Template migration | Batched async I/O, cached serializers, source-generated contexts, checkpoint/resume. |
| Git wrapper | Async process/file I/O, bounded diff/status operations, cancellation, no full tree scans per UI refresh. |
| UI projection queries | Server-side filtering/paging/windowing, cancellation, no load-all then client filter. |
| Canvas projection builders | Single-pass transforms, pre-sized collections, deterministic layout, no repeated full recomposition during trivial UI state changes. |
| External adapters | `IHttpClientFactory`/typed clients, streaming large responses, cancellation, backoff, bounded parallelism. |

## Async And Concurrency Rules

- Runtime, dispatcher, manager, projection, persistence, Git, template, and adapter APIs that perform I/O must expose async methods and accept `CancellationToken`.
- Sync-over-async is forbidden: no `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, or synchronous wrappers over async implementation.
- `Task.Run` is forbidden in library/runtime code as a fake async wrapper. It is allowed only in clearly isolated UI/application orchestration when deliberately offloading CPU work and when cancellation/progress behavior is explicit.
- Producer/consumer paths must use `System.Threading.Channels` or an equivalent bounded queue abstraction.
- Queues must have capacity and overflow policy. Unbounded queues are allowed only with a written proof that the producer is naturally bounded.
- Background workers must be lifecycle-managed and observable. No fire-and-forget tasks without registration, cancellation, exception handling, and health reporting.
- `ValueTask` is allowed only for hot-path APIs with frequent synchronous completion or framework-required signatures. Do not use it as a blanket replacement for `Task`.

## Collections And LINQ Rules

- LINQ is allowed in cold-path authoring, admin, and test code when clarity is better.
- LINQ is restricted in runtime, dispatcher, projector, live snapshot, artifact ledger, branch routing, manager loop, and canvas projection hot paths.
- Hot paths must prefer explicit loops, `TryGetValue`, pre-sized collections, and single-pass transforms.
- Do not enumerate an `IEnumerable<T>` more than once. Materialize once or rewrite as a single-pass loop.
- Use `TryGetNonEnumeratedCount` or known counts to pre-size destination collections.
- Use `FrozenDictionary` and `FrozenSet` for read-heavy lookup tables created once and queried many times, such as capability registries, branch definitions, operation-policy maps, projection field maps, and template component indexes.
- Use `StringComparer.Ordinal` or `StringComparer.OrdinalIgnoreCase` for non-linguistic keys.
- Avoid `params` helper APIs in hot paths. Provide common arity overloads or use `ReadOnlySpan<T>` where the target framework supports it.

## Strings, Parsing, And Regex Rules

- Domain-neutral identifiers, branch route keys, artifact IDs, template keys, event types, and capability IDs must use strongly typed wrappers or enums where possible.
- Repeated parsing must avoid `Substring` allocations; use `ReadOnlySpan<char>` parsing helpers where it materially reduces allocation.
- Non-linguistic string comparisons must specify `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase`.
- Static regex patterns must use `[GeneratedRegex]`.
- User-controlled regex patterns are forbidden in core/runtime paths. If a driver explicitly allows a pattern, the driver must use bounded/non-backtracking behavior and policy validation.
- Branch routing must not use regex or free-text token matching as the primary route mechanism.

## JSON And Serialization Rules

- New Process projects must use `System.Text.Json` source-generated contexts for:
  - process templates,
  - template migration reports,
  - process exchange envelopes,
  - runtime event envelopes,
  - snapshots and UI projections,
  - artifact ledger records,
  - Git metadata and audit records.
- Do not allocate `new JsonSerializerOptions(...)` per call. Options are static cached values or generated context options.
- Template migration must stream or batch large reads and writes. It must not load all process/template/configuration files into one object graph.
- Event replay and projection rebuild must process events in batches with checkpointed offsets.

## Candidate Readiness Performance Rules

- `RoleExecutionRequirementSet` is compiled once per launch role and reused for all candidates until the launch plan or role requirements change.
- Candidate readiness uses an `EvidenceSnapshotHash` so repeated UI refreshes do not refetch unchanged HR, agent, workflow, provider, rights, project assignment, and tool availability evidence.
- Candidate discovery may fan out to multiple registries, but readiness evaluation must batch shared evidence by candidate kind, project scope, provider, workflow catalog, and rights source where possible.
- Readiness findings are built from typed requirement/evidence comparisons, not repeated text parsing of HR summaries.
- Reassessment after provisioning loads a fresh evidence snapshot for affected candidates and requirements only.
- Launch projections store the assessment summary needed by the UI so components do not recompute readiness from raw evidence.

## I/O And External Adapter Rules

- File I/O in template, Git, artifact, projection, and migration paths must use async APIs where the operation can block.
- Large file reads/writes must use streaming and bounded buffers. Use `ArrayPool<T>` only when ownership and clearing rules are simple and tested.
- External HTTP integrations must use `IHttpClientFactory` or typed clients. Creating `HttpClient` per call is forbidden.
- Large HTTP responses must use `HttpCompletionOption.ResponseHeadersRead` and stream processing.
- Git wrapper operations must avoid full repository status/diff scans on every UI refresh. Cache/index results with invalidation and explicit refresh.

## UI Performance Rules

- Process UI components must consume bounded projections. They must not load all runs, all events, all artifacts, all definitions, or all template files and then filter locally.
- Live/history views must apply time-window and process filters in projection/query services.
- Definition/run/template lists must use paging, continuation tokens, or virtualization when result counts can grow.
- Canvas rendering must use stable projection DTOs and stable dimensions so updates do not force full recomposition unless graph topology changed.
- Blazor components must keep non-trivial projection shaping in services/presenters, not lifecycle hooks.
- UI refresh must be cancellation-aware and must not trigger runtime recomputation.

## Structural Rules

- Leaf implementation classes are sealed by default.
- Interfaces are used for real boundaries: runtime ports, persistence ports, driver/strategy contracts, adapters, application services, and test seams.
- Avoid interfaces for trivial single-implementation helper services unless the boundary is needed for tests or future extension.
- Keep hot-path classes small enough to benchmark and unit test.
- Split pure policy/rule code from I/O adapters so tests can exercise behavior without storage/network overhead.

## Required Future Scan

Every subbundle that creates or modifies C# hot-path code must include a performance scan section in its execution report:

```text
Files scanned:
async void:
Task.Result / .Wait / GetAwaiter().GetResult:
Task.Run:
ValueTask:
Substring:
StringComparison omissions:
ToLower/ToUpper empty overload:
RegexOptions.Compiled:
GeneratedRegex:
new Regex:
new List:
new Dictionary:
LINQ chain candidates:
new HttpClient:
new JsonSerializerOptions:
unsealed leaf class candidates:
sealed classes:
Accepted tradeoffs:
```

The report must include exact counts and explain every accepted hot-path tradeoff. Counts of zero are valuable and must be recorded.

## Stop Conditions

Future implementation must stop and report if:

- a runtime/dispatcher/manager/projector path requires sync-over-async to work,
- a projection or monitoring path needs an unbounded queue to avoid data loss,
- a UI story requires loading all history/events/runs/artifacts into the browser,
- template migration cannot run in bounded batches,
- a branch route requires free-text regex/string token routing in generic core/runtime,
- candidate readiness requires repeated per-candidate external/provider calls where shared evidence can be loaded once,
- serialization requires per-call `JsonSerializerOptions`,
- an external adapter requires per-call `HttpClient`,
- performance scan counts reveal broad hot-path LINQ or collection allocation and no mitigation is planned.
