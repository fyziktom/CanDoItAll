# Atomic Process refresh delivery and returned-work wake

Status: Pass for this Behavioral persistence checkpoint. All 69 selected cases, direct
builds, final portability, source/output identity, secret review and new resource closure pass.
This extends the qualified [original continuation](2026-10-23-process-refresh-delivery.md).
No worker, public refresh/Replan activation, legacy adoption or capacity claim.

## Entry and ownership

The user's concurrent user/agent/workflow requirement needs admitted work to survive a
crash between admission and delivery, without duplicating capture or losing its returned
work. The [P28 entry decision](../plan/P28-process-refresh-recovery.md#queue-persistence-entry-after-continuation-qualification)
fixes the boundaries before implementation. Entry gate: Pass for queue persistence.
Signed `1ae54a4f8` and `725c880e4` establish the continuation and its 110 selected cases.
The previously denied `C:\repositories\jrc` junction cleanup remains independent and blocked;
the exact user-authorized retry was rejected by automatic review as **blocked by policy**.

Application owns the typed claim, checkpoint and persistence port. Persistence owns one
small scheduling row, its SQL leases, admission staging and atomic Return wake. The module
registers the port; existing stop/account/capture owners remain the business authority.
No project reference, Runtime command wire, generic queue framework or partial expansion.
The table has no JSON/material payload and does not read runtime history during claims.

A new refresh admission stages its row beside its original receipt in the runtime UOW.
Replay preserves scheduling progress and never adopts an older admission. Claim selects
one due Ready row with SKIP LOCKED. Checkpoint compares original root/run/admission,
token/generation/progress and database-time expiry; it always releases ownership. A late
capture Return increments the wake revision inside its own root-fenced transaction.
An older pending checkpoint cannot park over that wake. Active leases retain their due
timestamp, so waking cannot create a second live delivery owner. Terminal/review outcomes
leave the due index. Queue outcomes never settle budgets or authorize invocation.

Operations/transactions are bounded at five seconds, statements at two seconds and lock
waits at one second. Queue claims/checkpoints acquire no root fence; Return takes root
then queue-row locks, with no reverse path. These are SQL/operation bounds, not measured
end-to-end delivery deadlines or sustained capacity. The existing continuation still
loads original receipt/runtime state; this checkpoint does not claim constant-history
cost for that business flow.

Migration `20261024003418_AddProcessRefreshDelivery` adds one table, its exact constraints,
receipt/root foreign keys and a partial due index. Down takes an exclusive table lock and
refuses any retained delivery evidence, including completed rows. Generated migration
formatting follows local style; the existing snapshot retains unrelated formatting.

## Validation selection

Proof root: `.artifacts/concurrent-mutations/validation/c4-process-refresh-delivery-queue`.
Ownership precedes creation. Reuse the verified isolated PG18 server on 53619 and its
bounded database leases; preserve ordinary port 5032 and the existing fixture for later
lanes. Serialize standard Release builds with local family libraries, `/m:1`,
`/nodeReuse:false` and `UseSharedCompilation=false`. Build Application, Persistence,
Modules.Processes, Migrations.PostgreSql and Integration directly. Migration generation
uses the built design-time project, then refreshes the standard outputs.

All filters use `FullyQualifiedName~CanDoItAll.Tests.Integration.<class>.` in
`tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`:

| Class | Expected | Owned behavior |
| --- | ---: | --- |
| ProcessDecisionRefreshDeliveryQueueTests | 14 | Registered admission replay, distinct/busy claims, expiry during checkpoint, stale identity, lost acknowledgement, typed outcomes |
| ProcessDecisionRefreshDeliveryWakeTests | 16 | Both snapshot modes, Return/park orderings, cancellation, actual SQL row-lock race, atomic Return/payload/wake rollback, review retention |
| ProcessDecisionRefreshDeliveryMigrationTests | 3 | Empty roundtrip, retained-evidence downgrade refusal, constraints/FKs/index |
| ProcessDecisionRefreshPersistenceTests | 17 | Actual admission/UOW atomicity, competing/replayed requests, rollback before/after flush |
| ProcessDecisionRefreshCaptureJournalPersistenceTests | 16 | Changed journal transaction, capture/settlement, unknown retention and restart |
| ProcessDecisionRefreshCaptureJournalMigrationTests | 3 | Existing journal migration and constraints beside the new table |

Expected total: 69; discovery must match before execution. Queue-only ownership tests
seed scheduling metadata against ordinary runtime receipts to isolate lease mechanics.
They do not claim refresh authorization. Actual registered refresh producer tests and
both-mode capture/wake flows establish that business boundary. A shallow implementation
that enqueues after commit fails rollback proof; one that parks blindly loses an original
Return; one that equates lease expiry with invocation permission duplicates capture.

Changed owners and the additive migration define this affected selection. No global
composition, serializer version or project-reference change justifies an unfiltered
Stable run. Final portability includes new protected files; require no-write enforcement,
source/output identity, secret review, architecture and owned-resource reconciliation.

## Architecture and execution notes

Snapshot `snap-20261024004134-6b7e48c7` confirms six scoped project edges and no cycles
across 525 documents, 1,350 types, 10,888 members and 37 entities. Three new informational
member-count findings cover cohesive claim/enum/row data; the architecture gate records
their review. Existing NuGet audit diagnostics limit the collector's compilation claim;
direct builds passed, and no non-audit error was reported by the collector.

Observer setup initially refused a missing copied deadline helper before launching a
process, then refused incompatible ownership field names before starting observation.
The original before-create ownership record is preserved with its hash. Its equivalent
root/state/time aliases and the exact existing helper were restored before any test.
The original pre-run resource baseline remains verified; observer attempt 2 covers the
actual execution. Both startup failures remain explicit proof, not test failures.

Discovery matched all 69 cases. The queue's 14 cases pass in 94.562 seconds, the wake's
16 in 224.797 seconds and the migration's three in 28.037 seconds. These are test durations,
not capacity measurements. Final source and output checks pin 1,308 inputs and 32 standard
Release outputs. Application/Persistence/module direct builds pass; final migration and
Integration confirmations take 45.823 and 35.671 seconds.

The complete portability scan covers 10,171 files and 47,449 findings without truncation,
including new protected sources. Six new allowances are reviewed generated EF metadata:
RootPathState/RootPathSyntax, existing workflow recovery index names, RaisesEscalation
and EscalationCount. Their expressions match the previously reviewed migration model;
the new file path produces new fingerprints. The baseline diff contains exactly those
six allowances plus timestamp/count (15,703 to 15,709), with no policy change or stale
entry. Final enforcement without `--write-baseline` passes. Portability/secret/rendering
tool tests, rendering register, documentation and whitespace checks pass. Reviewed bulk
scan JSON/CSV files were removed separately after exact path/hash checks; the compact
summary and enforcement logs remain. Final secret review passes, including supplementary
excluded files and the private fixture sentinel; only three exact reviewed script
expressions remain. The initial static refusal and both observer setup failures are retained.

The existing admission cases pass 17/17 in 216.718 seconds, journal persistence 16/16 in
222.500 seconds and journal migration 3/3 in 52.315 seconds. All 69 cases ran on the pinned
candidate without a source/test repair. Signed implementation checkpoint
`f4a51c39a9ad8954f7f11de884278d17f9bb0bea` retains those exact source and output identities.

The successful observer covers all six execution commands. Final reconciliation finds
zero remaining owned test processes, database-name changes or temporary-path deltas.
All command records reconcile, with the observer setup refusal and first baseline
enforcement refusal explicitly classified as repaired expected failures. The shared
isolated PostgreSQL fixture remains for subsequent lanes; no replacement stack or host
was created. Ordinary port 5032 and historical blocked resources remain preserved.

Closure gate: Pass for atomic scheduling, lease/checkpoint ownership and original Return
wake. The subsequent worker can rely on these invariants after satisfying the remaining
activation gates below. This does not close the user's whole-engine E2E requirement.

## Open downstream gates

Background delivery, retry/backoff policy, whole-continuation deadline/concurrency limits,
source/profile revocation, explicit unknown-work resolution and retry authorization,
retained-admission adoption and actual mixed/older-writer rollout remain open. A previous
writer does not publish the new wake revision; older-reader retention proof alone cannot
qualify that deployment. Measure due selection with retained terminal history, fairness,
unrelated-root progress and payload memory before worker activation. Broader engine E2E
requirements and historical cleanup denials remain visible in the requirements ledger.
SPA stays on `codex/concurrent-mutations` for colleague review.
