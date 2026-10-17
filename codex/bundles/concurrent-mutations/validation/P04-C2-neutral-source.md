# C2a authored source and remaining proof

Status: source preparation in `C:/Users/lucys/.codex/worktrees/concurrent-mutations-runtime/CanDoItAll` only. No build, test, static scan, migration generation, database command or performance run was executed by this author. The parent owns integration and validation. This record is not a runtime pass or permission to activate another owner receipt family.

The implementation follows [the extraction card](../plan/P04-C2-neutral-epoch-extraction.md). The [frozen file inventory](P04-C2-source-inventory.json) names the source changes and explicit deletions. It includes mechanical adaptation of existing tests and opt-in performance tools; these are not new successful measurements.

## Production boundary

- Infrastructure owns two neutral metadata records, their context, finite pool/family contracts, epoch admission/rotation/retirement, separately enlisted family maintenance, and bounded scheduling/result/health state. It has no Projects/Workbench/Workspace reference, receipt codec, owner receipt entity, authorization callback, FK, cascade or trigger.
- Projects owns its fixed pool/family registration, the unchanged receipt entity/payload/digest, receipt-specific purge/inspection SQL, operation exclusion key and one-statement receipt lookup. No caller wire type or accepted-payload version changed. H2's current-write observation and commit-uncertainty repair remain in place.
- Composition owns the one hosted worker and explicit pool registration. The worker retains the 60-second default, validated 5–60-second range, two-second per-pool budget, fixed cursor progression and low-cardinality pool/family metrics. No second worker remains. Metadata maintenance uses separate family transactions before the neutral-only final delete.
- Each binding observation opens a scheduling pass of at most the installed pool count. Due times and family cursors survive a repeated matching binding, but elapsed work cannot keep an inner pass alive indefinitely; the worker must observe the profile/binding again. The pure scheduling Fact includes three five-second pools with two seconds of work per turn. Metadata eligibility also observes remaining inactive empty states after the bounded delete; the existing concurrent-cleanup Fact proves a 129-state backlog reports more work after deleting 128, then clears after the final state.
- Bootstrapper construction resolves the actual maintenance validator before any bootstrap SQL. The maintenance service and Projects participant are singleton: immutable pooled options, immutable registrations, the existing singleton `AsyncLocal` transaction coordinator, and fresh per-call contexts. The epoch store stays scoped. Constructors do no SQL or provider activation; future participants must satisfy this same lifetime constraint. Offline activation still uses compiled metadata registrations only.
- Active readiness validates existing metadata and fails on missing/corrupt state. Initialization belongs only to explicit Bootstrap/Reactivate or a future stopped-host pool-installation migration. Reactivation closes previous namespaces in the same transaction as the new binding and new pool state; ordinary startup never repairs issued counters.

## Source-derived tests

These counts come from the final authored attributes, not runner enumeration. Neither new class has traits, theories, dynamic data or inherited tests.

| Test project / class | Discovery | Execution | Meaning |
| --- | ---: | ---: | --- |
| Unit / `FiniteOperationContractTests` | 8 | 8 | IDs and immutable registration; timing/batch bounds; actual bootstrapper DI with valid/missing/duplicate participants and both DI validators enabled; participant observations; inspection subset/copy; pool/family cursors; neutral model boundary; issued snapshot invariants. |
| Integration / `FiniteOperationPersistenceTests` | 12 | 12 | Real PostgreSQL metadata/readiness, multi-pool transactions, family inspection, concurrent purge/cleanup and lookup snapshot arrangements described below. |
| Parent-owned migration-specific new class | 4 planned | 4 planned | Not authored here; not included in the 20 authored rows. |

The 12 PostgreSQL Facts cover:

1. Matching Active EnsureReady/Attach/Resume fail on missing issued state; direct admission rejects an old Open/noncurrent epoch; unsupported protocol and missing retained current epoch fail explicitly.
2. Two-pool activation/closure rolls back atomically, and repeating the exact new binding preserves issued values. This arrangement proves SQL enlistment; the existing coordinator tests retain responsibility for Prepared file/SQL recovery.
3. A missing newly installed pool refuses readiness; explicit initialization persists the original promise across tighter later options.
4. A held state lock in one pool does not block another pool's admission/rotation; the blocked pool returns Busy.
5. Nonempty or failed family inspection retains metadata until all declared families are empty; each family uses a different transaction.
6. The actual PostgreSQL lifecycle-EXCLUSIVE/table-A/table-B import lock sequence completes against family-B then family-A inspection, because each inspection releases its transaction. This reproduces the runner's lock order directly; it is not a call through the complete business import facade. Existing target-transfer lanes remain required.
7. Concurrent metadata workers delete one eligible epoch once. The same real Projects participant and maintenance singleton are also invoked concurrently behind a SQL barrier: two different enlisted transactions, exact combined receipt/byte counts and no shared context.
8. Binding or protocol changes between inspection and final metadata deletion cause explicit refusal and rollback.
9. Late admission into a retired epoch cannot insert a receipt after the empty inspection.
10. A failed family does not suppress another family's committed counts; a later metadata failure remains Unknown/Failed.
11. Budget cancellation after one acknowledged batch preserves counts and advances the family cursor so the unserved peer runs first next cycle.
12. A paused one-statement Projects lookup retains its original receipt bytes/watermark across concurrent retirement/purge; a fresh lookup returns Expired, not invented ClosedAbsent.

The prior card's **85 existing rows** remain a separate regression selection (24 Unit + 61 Integration). Their source row counts are unchanged. Mechanical constructor/metadata adaptations in additional transfer, component, performance and capacity fixtures do not establish those lanes as executed. In particular, long-running performance/capacity/process-host lanes remain opt-in and are excluded from the default focused execution selection.

## Migration and performance gates

The model contains exactly two neutral entities in place of the two removed Projects metadata entities; the Projects owner context now has seven types. Historical migrations are untouched. The parent will generate/review the lossless rename/composite-key migration and canonical snapshot, add preflight/postflight data correspondence and unexpected FK/trigger checks, and implement explicit Down refusal. Its four migration Facts must prove retained bytes/times/counters, all namespaces, fresh initialization, rollback of failed preflight and exact historical replay. No drop/recreate scaffold may be accepted as the final migration.

The new maintenance workload label is `neutral-pool-separated-family-retention-v3`. Earlier protected-write throughput and finite-backlog observations do not certify this extraction's added maintenance transactions or future multi-owner capacity. The current capacity host explicitly accepts only the fixed Projects pool/definition family; it validates the renamed instruments and rejects unrelated identities. The production metrics also expose scheduled lateness. Sustained arrivals, two-process maintenance, retained storage/WAL, oldest eligible work and default-cadence recovery remain required measurements. No throughput improvement is claimed.

The B1 lifecycle test repair is separate and independently transferable: [live lock-manager observation](../reviews/P04-B1-lifecycle-wait-observation-repair.md). It does not change B1 production behavior or test counts.
