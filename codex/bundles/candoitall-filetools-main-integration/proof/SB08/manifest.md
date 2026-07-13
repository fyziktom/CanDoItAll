# SB08 Governed Proof Manifest

Date: 2026-07-12. Closure decision: `Pass`.

## Scope And Provenance

- Production scope is the process-local file-catalog revision reader/change sink, optional raw-provider listing decorator, canonical hashed key builder, exact UTF-8 cache DTO, HybridCache memory-primary store, bounded retention metrics, save/placement revision producers, and explicit composition wiring.
- `Microsoft.Extensions.Caching.Hybrid/10.0.0` is referenced only by `CanDoItAll.FileTools.Integration`. Infrastructure, Abstractions, Web, modules, components, and drivers contain no HybridCache type.
- `source-hashes.sha256` records every changed SB08 source, project, composition, and test owner from the final verified state.

## Evidence Index

| Evidence | Purpose | Result |
| --- | --- | --- |
| `semantic-invariants.md` | Named disabled, isolation, authority, boundedness, freshness, producer, and distributed invariants | Pass |
| `transcripts/failing-first-domain-serialization.txt` | Shallow direct-domain-object cache serialization failure | Correctly failed 4 cache tests; repaired with bounded DTO bytes |
| `transcripts/passing-cache-revision.txt` | Cache, revision, mutation, config, authority, and package-boundary tests | Pass: 39 tests, 0 failed |
| `transcripts/passing-host.txt` | Real host/DI/security regression after package restore | Pass: 8 tests, 0 failed |
| `transcripts/source-architecture-audit.txt` | Build, format, dependency, bounded-work, logs, and CodeAnalytics gate | Pass |
| CodeAnalytics `snap-20260713051010-baab347b` | Exact final scoped architecture review | Pass: 5 projects, 426 types, 2,586 members; no affected warning or new cycle |

## Cache Model And Key Matrix

- The decorator caches only raw safe provider listing facts. It never caches handles, authorization decisions, actors, grants, streams, content, secrets, signed URLs, or effects.
- Actor/grant identity is intentionally absent from raw-provider keys: the facts may be shared, but every activation/content/save effect is independently re-resolved and authorized against the current context. A test keeps a stale cached listing, removes the native occurrence, and proves authorization fails before grant.
- The fixed-length SHA-256 key binds schema, runtime profile/fingerprint/generation, storage/provider, semantic scope, binding root, ordered source-set fingerprint, storage configuration fingerprint, storage/scope revisions, container, continuation, page size, sort, metadata, and every native work-budget dimension.
- Raw path/config/query values never appear in the emitted key or logs. Semantic IDs, provider config JSON, endpoint/root, source count, container, cursor, and fingerprint lengths are explicitly bounded before hashing.

## Retention And Lifecycle Matrix

| Artifact | Bound | Lifecycle | Observable proof |
| --- | --- | --- | --- |
| HybridCache value | Exact serialized UTF-8 bytes, per-entry maximum 4 KiB-16 MiB | Local memory only; distributed read/write flags disabled | Oversized payload bypass test; actual byte length drives admission |
| Per-storage partition | Configured entries, items, continuations, retained bytes, TTL, hard lifetime | Oldest-access deterministic eviction under serialized admission gate | Metrics expose hits/misses/bypasses/evictions and retained counts/bytes |
| Process-wide store | 10,000 entries, 50,000 items, 10,000 continuations, 256 MiB | Global cap across all storage partitions | Source assertion plus bounded counters; no unbounded UI/session snapshot |
| Coalesced load | One HybridCache factory per key | Cancelled/failed factories retain no entry; callers receive explicit failure/cancellation | Concurrent same-key test performs one native call; failure/cancel retry calls native again |
| File catalog revision | Two monotonic values: storage-wide and semantic-scope/storage | Process-local; runtime/profile generation is a separate key dimension | Successful save bumps scope after persistence; successful placement bumps storage after persistence; failure/cancel do not bump |

## Policy Decisions

- Missing/legacy configuration is literal Disabled: the decorator invokes the native driver directly and performs zero cache-store lookup, write, coalescing, metric-bypass, or retention work.
- Only `Memory` mode executes. `Hybrid` remains a typed invalid configuration because no durable shared revision/backplane exists. Every entry additionally uses `DisableDistributedCache`, preventing an unrelated future `IDistributedCache` registration from silently enabling L2.
- Provider immutable policy is accepted only for an IPFS `cid:` binding whose driver advertises immutable version support. Mutable MFS is rejected even if the shared IPFS driver advertises that capability.
- FileBrowser session retention remains unchanged/disabled by the integration boundary; this phase adds host listing caching only.

## Architecture And Performance Decisions

- Decorator, DTO, key, policy, metrics, and revision implementation live in outer Integration. Infrastructure only retains the typed configuration and the concrete placement registration needed for explicit composition.
- Storage placement revision composition is a separate checked extension. It requires exactly the expected Infrastructure placement registration and fails on missing/ambiguous/custom composition instead of silently replacing it.
- No cache type appears in a provider driver, component, page, Web adapter, or Integration.Abstractions. No service locator, partial class, sync-over-async, raw sensitive log placeholder, or unbounded source enumeration was added.
- Largest affected implementation owners: `StorageFileBrowserProvider` 291 lines, handle registry 278, cache store 251. Final CodeAnalytics reports only informational member-count findings in affected code.

## Build And Regression

- Final affected Release run: 39 passed, 0 failed across SB08 cache/revision/config/placement plus SB06/SB07 boundary/security regression.
- Final real ASP.NET host run: 8 passed, 0 failed. The first run used a stale integration-test assets graph and could not load the new package; explicit restore refreshed the graph and the unchanged tests passed.
- Web Release warnings-as-errors build: 0 warnings, 0 errors.
- Focused format plus `--verify-no-changes`: Pass.
- Final dependency graph is unchanged: Composition -> Integration/Infrastructure; Integration -> Abstractions/Infrastructure; Web -> Composition/Integration/Infrastructure. The existing Infrastructure Persistence/ControlPlane module cycle is unchanged.

## Downstream And Progression

- The aggregate smoke observes `before.txt`, remains intentionally cached before publication, then selects `after.txt` immediately after a successful semantic revision bump.
- SB08 closes and unlocks SB09. Any later disabled-path cache call, cross-runtime/source collision, cache-authority use, unbounded payload, failed-mutation bump, distributed fallback, or stale post-revision selection reopens SB08 and dependent UI proof.
