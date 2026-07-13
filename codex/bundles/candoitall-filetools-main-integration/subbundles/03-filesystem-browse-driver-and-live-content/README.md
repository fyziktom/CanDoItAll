# SB03 Filesystem Browse Driver And Live Content

## Status

- `Completed`

Governed proof passed 2026-07-12.

## Objective

- Implement secure, fresh, bounded filesystem browsing and content access over the native Storage contract without duplicating path policy or introducing cache.

## Covered Inputs

- N002-N004, N006-N008, N014-N015; R005, R009-R013, R026-R036, R040.

## Prerequisites

- SB02 Completed with trusted contracts/settings/registry proof.

## Exact Source References

- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Placement/StoragePlacementService.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LocalFileStorageTests.cs`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.Providers.FileSystem`
- `bundle://architecture/10-performance-and-scale.md`
- `bundle://analysis/03-dotnet-performance-audit.md`

## Deliverables

- Focused `FileSystemStorageBrowseDriver` and cohesive shared path policy used by existing and browse paths where practical.
- Root/path/browse, deterministic bounded paging, current metadata, cancellation, safe errors/logs, and current content read/stat needed by outer adapter.
- Large-directory implementation that does not enumerate/snapshot/hash/global-sort all children before page one. Ordering is provider-native or an explicitly bounded index; unsupported ordering/budget exhaustion is typed and honest.
- Instrumented scale harness with small and at least 100,000-direct-entry fixtures where supported, recording inspected entries, metadata calls, retained state, allocations, cancellation, and repeated latency.
- No reparse traversal, absolute-root disclosure, provider/host cache, shell effect, or write authority in browse results.
- Existing save/read/delete/placement behavior preserved by characterization.

## Dependency Impact

- SB05/SB07/SB10 depend on filesystem confinement/freshness. Weak proof invalidates the pilot and every live process/workspace flow.

## Validation Depth

- Proof tier: `Governed`.
- Critical security/freshness foundation; create `bundle://proof/SB03/manifest.md` and semantic invariants during execution.

## Implementation Steps

1. Add failing-first traversal/reparse/stale-mutation/paging/error-redaction tests plus a regression proving current FileTools-style full enumeration/sort/hash is unacceptable.
2. Inventory current path algorithms and extract one focused collaborator only if it removes duplication.
3. Implement budgeted shallow paging, root/path facts, query-bound cursor, requested-only metadata, cancellation, diagnostics, and typed partial/unsupported failures.
4. Implement current read/stat bridge independent of a browser session; do not add save here.
5. Register as native browse driver.
6. Run existing storage regression plus new security/freshness and repeated structural scale suite.
7. Rerun the scoped performance scan for changed hot paths; fix or measure every actionable hit.
8. Refresh CodeAnalytics and source assertions.

## C# Architecture Impact

- Local provider responsibility extraction; old `FileSystemStorageDriver` must not grow into a browse monolith.

## Boundary Ownership

- Infrastructure filesystem provider only; semantic project/run authorization stays outer.

## Dependency Direction

- BCL/Infrastructure only; no FileTools/UI/Web reference.

## Pattern Decision

- PSR-02 provider adapter and focused path collaborator; no inheritance hierarchy.

## Testability Contract

- Direct temp-root tests instantiate browse driver/path collaborator. Full host is integration-only.

## Partial Class Policy

- No partial or nested provider boundary.

## Architecture Proof Required

- Before/after responsibility/line map, direct unit tests, old-driver behavior proof, no-new-partial, no-cache/effect/source assertions, CodeAnalytics result.

## Scope Exceptions

- Trusted configured root remains an OS trust boundary; no claim of hostile-root handle-relative no-follow safety.

## Do Not Do

- Do not follow links, catch-and-return empty, expose raw exception/path, cache listings, infer authorization from containment, use `GetFiles/GetDirectories`, or materialize/order/hash an unbounded child set before page one.

## Acceptance Checklist

- [x] Traversal/reparse/root disclosure negatives pass.
- [x] Bounded deterministic pages and stale cursor behavior pass.
- [x] Page-one inspected entries, metadata calls, memory/state, and cancellation stay within declared budgets at large cardinality.
- [x] Mutation/replacement is visible on next Disabled read.
- [x] Cancellation/failure publishes no partial success.
- [x] Existing filesystem storage behavior remains green.

## Proof Required

- Governed manifest, hashes, failing/passing transcripts, semantic invariants, anti-stub/source/log assertions, dependent outer-adapter live-read smoke.
- Shallow-pass trap: a provider returns only `PageSize` items but first enumerates/sorts/hashes every child. The failing-first large-directory test must detect this through inspected-entry/metadata/state counters, and the realistic positive must return correct first/next pages from at least 100,000 direct entries within declared bounds.
- The governed production artifact matrix covers the operation diagnostics producer, metrics/log consumer, cancellation/continuation lifecycle, and a negative proving fixture-seeded counters cannot substitute for production instrumentation.

## Browser Validation Logging

- N/A here; dependent browser proof occurs SB10 and is cited as downstream check.

## Progression Gate

- SB05 may trust filesystem only after governed proof and one adapter-level mutation/read smoke pass.

## Reopen Triggers

- Later traversal, stale read, path leak, provider cache, symlink ambiguity, duplicated path policy, O(total-children) page one, or N+1 metadata outside budget reopens SB03 and invalidates SB05-SB18 filesystem proof.

## Suggested Agent Prompt

```text
Implement and govern the native filesystem browse/content boundary only. Start with adversarial tests, keep it live and uncached, preserve existing storage behavior, and stop on any security or path-policy ambiguity.
```
