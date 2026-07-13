# SB08 Host Cache And File Catalog Revision

## Status

- `Ready`

## Objective

- Add optional bounded host listing cache and process-local semantic revision while preserving true Disabled behavior, authorization/runtime isolation, and honest distributed limitations.

## Covered Inputs

- N004, N008, N014-N015; R007, R013-R014, R020-R021, R023, R028-R036, R040.

## Prerequisites

- SB07 Completed; current authorization/handle/effect proof trusted.

## Exact Source References

- `bundle://architecture/07-cache-and-revision.md`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Models/StorageModels.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Placement/StoragePlacementService.cs`
- `bundle://subbundles/06-filetools-package-adoption-and-integration-boundaries/README.md`
- `bundle://subbundles/07-authorized-handles-content-save-and-endpoint-hardening/README.md`

## Deliverables

- Typed policy resolver, HybridCache memory-primary decorator, canonical hashed key builder, in-memory monotonic revision service/change sink.
- Explicit entry-count/byte/TTL/coalescing/continuation-retention bounds and observable eviction; no unbounded result snapshot or raw file-content caching.
- Disabled path performs no lookup/store/coalescing and FileBrowser session retention remains Disabled when required.
- Authorization-scoped versus raw-provider cache model is explicit per entry/decorator.
- Revision producers publish only after successful persistence; runtime/profile/source/grant changes isolate/invalidate.
- Distributed/Hybrid-secondary enablement fails closed without durable/shared revision.

## Dependency Impact

- Project/resource aggregate and live process semantics depend on this. Isolation/freshness errors invalidate SB09-SB18.

## Validation Depth

- Proof tier: `Governed`.
- Privacy/freshness/cross-request boundary; require `bundle://proof/SB08/manifest.md` and semantic invariants.

## Implementation Steps

1. Add failing-first Disabled-call-count, cross-context collision, failed-mutation revision, runtime-switch, distributed-mode, entry/byte bound, expiry/eviction, and cancelled-coalescing tests.
2. Implement validated policy/settings consumption.
3. Implement canonical key and cache decorator with explicit model.
4. Implement revision service/change sink and connect only proven producers.
5. Add integration/DI/runtime-switch tests and downstream project-scope fake smoke.
6. Run governed source/log/dependency/anti-stub proof and architecture review.

## C# Architecture Impact

- Decorator/policy/revision services in outer Integration; no cache in drivers/components/pages.

## Boundary Ownership

- Storage settings declare binding policy; integration executes; modules publish semantic changes.

## Dependency Direction

- HybridCache dependency only in Integration implementation; Abstractions/Infrastructure/UI remain free of its types.

## Pattern Decision

- PSR-03 Decorator; versioned keys are correctness, eviction is optimization.

## Testability Contract

- Deterministic fake inner provider/cache/runtime/clock; no live distributed service.

## Partial Class Policy

- No partial cache/revision owner.

## Architecture Proof Required

- Key/policy matrix, direct tests, producer/source assertions, package/reference/cycle result, no-cache-in-UI/driver audit.

## Scope Exceptions

- No distributed secondary/backplane/durable revision.

## Do Not Do

- Do not silently degrade Hybrid to Memory, use project/storage UpdatedAt as file revision, cache handles/streams/secrets/content, retain unbounded browser/search snapshots, or bump revision before persistence.

## Acceptance Checklist

- [ ] Missing config -> Disabled and zero cache interaction.
- [ ] Cross-actor/runtime/source/query entries do not collide.
- [ ] Mutable sources never claim immutable from config alone.
- [ ] Success bumps after persistence; failure/cancel does not.
- [ ] Distributed mode without prerequisite fails.

## Proof Required

- Governed manifest/hashes/transcripts/invariants, shallow-pass negative, producer-consumer-lifecycle matrix, anti-stub/source/log assertions, dependent aggregate smoke.

## Browser Validation Logging

- N/A; browser freshness is downstream SB10/SB14/SB15.

## Progression Gate

- SB09 enters after governed isolation/freshness proof and one authorized aggregate mutation selects a new revisioned entry.

## Reopen Triggers

- Stale live folder, cross-scope data/handle leak, unbounded payload, failed revision semantics, or distributed fallback reopens SB08 and affected UI proof.

## Suggested Agent Prompt

```text
Implement the governed optional cache and process-local revision boundary only. Prove Disabled is literal pass-through, keys isolate all authority/runtime/source/query dimensions, and revision changes only after successful persistence.
```
