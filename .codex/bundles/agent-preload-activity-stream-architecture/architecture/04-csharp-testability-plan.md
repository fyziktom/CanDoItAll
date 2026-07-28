# C# Testability Plan

## Contract seams

- A deterministic typed stream implementation accepts a capacity and clock where time affects cleanup.
- Agent activity coordinator accepts explicit stable partitions/operation IDs and a clock; per-operation leases hide envelope sequencing.
- Preparation loaders are injected and countable; prepared dependency revisions are supplied explicitly.
- Store/provider/runtime fakes can block at named gates to prove event ordering.
- Snapshot sources expose immutable current values and revisions without requiring a rendered component.
- Metrics collection is bounded and correlated.

## Failing-first tests

| Behavior | Test layer | Required negative |
| --- | --- | --- |
| First activity before catalog/provider/session work | Integration | Block catalog load; assert activity arrives and no run exists. |
| Ordered partition isolation | Unit | Concurrent publishes to two operations; assert no leakage and monotonic sequence. |
| Concurrent sequence uniqueness/fan-out | Unit | Multiple producer facts and readers; assert primitive-assigned unique order and no lost wakeups. |
| Bounded retention/gap/eviction | Unit | Overrun capacity and TTL/global partition limit; assert explicit gap/tombstone, never silent loss. |
| Late terminal replay | Unit | Subscribe after completion within TTL; terminal activity is present. |
| Immediate operation handle | Unit/component | Start returns stream ID before command completion; reader replays from sequence zero. |
| Active capacity | Unit | Active operations survive idle time; capacity containing only active partitions returns typed rejection. |
| Tombstone expiry | Unit | Evicted is observable within bounded TTL; after tombstone expiry/count eviction the result is `Unknown`. |
| Reader cancellation/disposal | Unit | Cancel one scoped reader; other reader and publisher remain healthy. |
| Lifecycle ownership | Unit | Duplicate bind/terminal is rejected; exactly one terminal CAS succeeds. |
| Approval continuation | Unit/integration | Initial operation ends suspended; new operation binds to same execution run. |
| Handler isolation compatibility | Unit | Throwing `ExecutionUpdated` subscriber cannot reverse persisted result or suppress later notification. |
| Shared-load cancellation | Unit | Cancel first and later waiters independently; shared preparation completes for remaining caller. |
| Preparation disposal/capacity | Unit | Scope disposal cancels shared work; multiple agents obey bounded per-key capacity. |
| Blueprint use-time validation | Unit/integration | Catalog/profile/provider changes after acquire reject/reprepare before policy/capability materialization; session/context never appear in cached map. |
| Blueprint immutability | Unit | Attempt downcast/mutation; previously acquired snapshot remains unchanged. |
| Invalidation fence | Unit | Complete old load after revision changes; stale result is rejected. |
| Snapshot race | Unit/component | Update module selection during capture; request sees exactly old or new complete revision, never a mixture. |
| Snapshot no write-back | Integration | Execute from stale snapshot; canonical domain version remains newer. |
| Snapshot identity separation | Unit | Identical republish advances publication revision only; content/selection changes content fingerprint only; coverage changes coverage fingerprint only; profile switch changes profile generation only. |
| Snapshot freshness | Unit/integration | Expired/profile-mismatched attachment returns typed unavailable and performs no implicit recapture/storage read. |
| Snapshot eligibility/coverage dispatch | Unit/integration | Vary source, scope, workspace, contributor, kind, concrete type, profile, freshness, and coverage independently; only the exact eligible request performs a zero-storage snapshot read, while only explicit canonical-current reads storage. |
| Prompt/tool representation | Unit | Bounded/redacted prompt and fuller authorized tool snapshot are atomically derived from one attachment publication/content fingerprint without leaking restricted facts into prompt text. |
| Attachment digest binding | Unit/integration | Hold prompt/content constant while varying publication, coverage, profile, or freshness identity; each independently changes the combined digest and approval continuation rejects a mismatched lease. |
| Typed attachment multiplicity | Unit | Two contributor-owned concrete attachment types round-trip opaquely through Core without module references/string keys/object dictionary. |
| Snapshot/write separation | Unit/integration | Snapshot attachment contracts cannot be supplied to mutation methods; agent writes enter the existing canonical service and a later projection refresh cannot write captured state back. |
| Process revision completeness | Unit | Enumerate every workspace/live emitted entity, position fact, prompt field, and tool field; surface selection, refresh, catalog, live summary/effective run, detail, record, history, event, files/agent focus, telemetry, and derived facts map to typed present/absent components, and changing a source changes only its declared component/derived fingerprint. |
| EF concurrency | Integration | Instrument contexts; assert independent tasks never overlap on one context instance. |
| Startup operation counts | Integration | Assert duplicate catalog/session reads are removed/coalesced. |
| Profile authorization/switch | Unit/component | Unauthorized partition is rejected; switch removes old reader/subscription state. |
| Process/floating UI | Component | Activity displays before run binding and through terminal/error/approval. |

## Performance matrix

- cold workspace without split indexes;
- warm initialized workspace/new scope;
- warm same scope/preparation hit;
- new versus existing session;
- no attachment versus small local attachment;
- minimal capabilities versus representative skills/tools;
- uncontended versus deliberately held file lock.

Capture:

- acceptance to first activity;
- acceptance to run binding;
- acceptance to runtime entry;
- runtime entry to first runtime progress;
- provider gate/dispatch/first semantic update when available;
- completion and reload;
- catalog/session/run-detail/provider read/write counts.

Use multiple iterations and record median/p95. CI gates ordering and counts, not fragile millisecond thresholds. SB05 records the explicit human go/no-go based on measurements.

## Producer/consumer/lifecycle matrix

SB02 proof must enumerate every producer and consumer, service lifetime, partition key, terminal behavior, overflow behavior, cancellation owner, error path, and disposal owner. Missing rows block the architecture gate.

## Anti-stub proof

- No activity implementation that only records a string or returns `Task.CompletedTask`.
- No blueprint containing only the existing `AgentDefinition` while claiming runtime preparation.
- No module snapshot test that injects prebuilt prompt text without exercising revision/capture.
- No UI test that sets status fields directly instead of consuming the stream.
- No performance claim based solely on local UI text or a mocked zero-delay store.

## Validation commands

Exact projects/filters are finalized per subbundle, but closure includes:

- targeted unit tests for stream, preparation, reference data, and architecture;
- targeted component tests for floating and process manager chat;
- targeted integration tests for execution tracking, file-store locking, provider EF query shape, and snapshot reuse;
- `dotnet build CanDoItAll.slnx`;
- relevant full test projects or solution tests as environment permits;
- CodeAnalytics dependency snapshots before and after;
- large-screen Playwright proof;
- one explicit `gpt-5.4-mini` provider-backed run after deterministic tests pass.
