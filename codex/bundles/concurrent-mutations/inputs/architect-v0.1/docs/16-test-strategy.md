# 16 · Validation strategy and proof boundaries

## No application tests were executed for this proposal

The [test catalogue](21-test-catalogue.md) and [JSON cases](../registers/test_scenarios.json) are proposed behavior specifications. Historical test names below are discovery seeds, not current discovered cases or passing results. Package tooling validation is reported separately from product validation.

## Test layers

| Layer | Proves | Does not prove |
|---|---|---|
| Pure policy/unit | Hash/equality rules, outcome mapping, bounded retries, base/submission reconciliation | Database isolation or actual UI wiring |
| Scripted host/session and real renderer | Draft/EditContext survival, generations, pending/conflict presentation | That the owner actually rejects a stale SQL mutation |
| Real PostgreSQL owner integration | Conditional updates, child/root/receipt atomicity, lifetimes, source protection | Asset behavior or external provider guarantees |
| HTTP and actual tool roundtrip | Original stamp/operation/basis survive real adapters and authorization | Soundness of model reasoning |
| Two-process concurrency | No accidental dependence on in-process semaphores | Arbitrary external systems' atomicity |
| Browser on real Web + API UI contract | User-visible conflict/reconciliation, actual request and owner result | Other untested viewport/runtime/platform behavior |
| Storage/provider fault tests | The specific supported provider protocol and uncertainty handling | Universal exactly-once delivery |
| Migration/lifecycle | Retained data, new epochs, imports, old clients and receipts | Production readiness without actual operational rehearsal |
| Load/capacity | Measured overhead and contention on declared workload | Correctness by itself |

## Deterministic concurrency before stress

Use barriers controlled by the test harness: after original read, after required guards, before conditional write, after SaveChanges before commit, after commit before response, before projection application, and after a source revocation. Each task must be awaited and failures must be observable. Avoid Thread.Sleep as the primary race mechanism.

For lost-update tests, use separate contexts and two independently loaded edit models. For database claims, run on actual isolated PostgreSQL with one or two host processes as appropriate. InMemory has no actual transaction atomicity in this product (SRC-05) and cannot substitute for that proof.

Include positive controls: the matching fresh write succeeds, legitimate duplicate returns the same accepted result, valid approved proposal publishes, and unaffected actors continue while a conflict is rejected. A system that rejects everything is not concurrency-safe functionality.

## Test discovery and execution workflow

Read the then-current full `docs/testing.md`, CI workflow and engineering instructions. Use available Code Analytics to identify affected callers/tests; preserve actual tool output. When unavailable, use symbol references, evaluated dependency graphs and explicit source inspection, and record that method. No fictitious MCP commands or successful discovery counts are supplied here.

Build each changed production project directly. Build the relevant test assembly for the current source. Determine expected discovery counts from the actual cases including parameterized rows; run `--list-tests` with the exact planned filter and compare counts. Zero or mismatch is a failed proof step. Only then execute that filter. `--no-build` is valid only against the matching built assembly (SRC-12/SRC-16).

Use the current owning test solutions, not the product solution as an implicit test suite: Unit, Components, Integration, Memory, Playwright and Stable have explicit entry points under `tests/Solutions`. Broader stable/platform runs occur at documented named invalidation triggers, frozen checkpoints and release/merge closure, not after every phase automatically.

## Candidate existing tests to rediscover

Historical coverage names `ProjectsWorkbenchDbContextTests`, `DatabaseCanonicalityArchitectureTests`, `OwnerBridgeRegistrationOrderTests`, `WorkbenchAssignmentOwnerPersistenceTests`, `ProjectWorkbenchServiceArchitectureTests`, `ProcessPreparedLaunchPersistenceTests`, `ProcessModuleBoundaryTests`, `StorageStablePlacementPersistenceTests`, `ToolOutcomeCompletionIntegrationTests` and `MafPolicyDenialCheckpointTests` (SRC-14). Re-find them on the actual branch and examine their assertions; do not merely append a broad substring filter and report "relevant tests passed".

Current component branch guidance also lists focused Collaboration session/renderer/host/integration/browser projects and filters (SRC-12/SRC-13). The extraction's historical case counts are not expected counts for future changed code. Preserve actual renderer/sandbox proof rather than introducing mocks that only echo a desired outcome.

## Required cross-cutting negative cases

Late save after selection A→B→A; typing during dispatched save; duplicate Enter/click; authority revocation before commit; old proposal with newly substituted token; membership phantom; same operation ID/different body; retry after confirmed commit and later unrelated change; lost commit acknowledgement; transaction disposal exception; source lease finalization after commit; delayed projection after delete/recreate; import with source live tokens; old-client bypass; direct bulk SQL child mutation; and stale compensation that would erase a newer change.

The catalogue assigns stable IDs, phases and invariants to these scenarios. Map each selected implementation slice to actual test names and evidence, and retain unimplemented scenarios as planned—not green.

## Browser proof

Use a disposable real host/database and two independent editors/browser contexts. Test human versus human and the actual governed agent tool versus a human. Inspect persisted owner state after the UI reports the result. Verify conflict text, surviving draft, fresh revision adoption, row identity, focus and no unintended dialog reopening. The API-only UI needs equivalent contract validation even if its repository/host is maintained separately; exact owner must be identified in P00.

Keep the backend-free scenario host for deterministic conflict/unknown/read-failure screens. It proves renderer behavior only. Follow existing bUnit async event/disposal guidance and browser fixtures rather than weakening timing assertions or quarantining a new failure (SRC-02/SRC-12).

## Environment and platform

Use isolated PostgreSQL 18 through the documented private connection variable, owned disposable databases and bounded cleanup. Never start or alter the development Compose stack as a fallback. Record sanitized endpoint/server version and matching SDK/sibling revisions. Do not log connection strings or terminate user processes. Windows/Linux/macOS obligations follow the actual current CI split and impact, including separate browser/live/provider lanes (SRC-12/SRC-17).

`portability-static` is required for protected changes: scan the complete prescribed tree, repair genuine defects, review intentional baseline deltas, regenerate after edits and enforce without `--write-baseline`. An affected-test pass does not waive that gate (SRC-01/SRC-16).

## Fault injection and observability

Prefer repository-supported fault points or narrowly scoped test-only adapters; do not add globally enabled production failure switches. Capture outcome provenance at commit and cleanup, not only the final exception type. Verify no leaked DbContexts, connections, locks, subscriptions or worker claims after repeated cancelled/racing operations.

## Test report contract

Record source/sibling/working-tree identity, exact command/filter, expected/actual discovered count, passed/failed/skipped results, environment, raw evidence location and remaining coverage. A requirement marked unavailable blocks its guarantee. Tests planned by this package remain status `proposed-not-executed` until actual evidence is entered in a future iteration.
