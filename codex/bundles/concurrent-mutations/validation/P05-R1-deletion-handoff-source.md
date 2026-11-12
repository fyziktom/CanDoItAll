# Project deletion handoff: authored source checkpoint

Stage 2 is authored in the managed checkout under work card `86A6A651`, release
`0CD74C8C`, and the [cleanup ownership correction](../reviews/P05-R1-deletion-cleanup-ownership-amendment.md).
This is unbuilt, unexecuted source. Stage 1's original `C96FC10B` inventory and its
terminal-drain successor `182F4784` remain separate and unchanged. Stage 3 shared
Resource membership locks, promotion exclusion and capacity remain held until the
parent validates this deletion path against PostgreSQL.

## Owner behavior and failure knowledge

`ProjectDeletionTransactionEntry` owns one fresh PostgreSQL connection. A short RC
prelude takes lifecycle SHARE before all distinct ordinal project/hierarchy/participant
session keys. It rolls back and disposes that prelude, begins Serializable on the same
backend, takes the lifecycle relation and row with NOWAIT before the first snapshot,
checks the original activation, takes matching transaction keys, unlocks session keys
in reverse order, and balances the extra EF open reference before returning. The
caller then performs its original source/admission/participant/Search reads and effects.
No business read or callback enters the prelude. Generic hierarchy, definition and
transfer entry modes retain their original isolation and waiting behavior.

Admission has one linked five-second budget, including source acquisition. Cleanup
SQL has an independent five-second cancellation/command budget. Noncancelable
disposal/close is cooperative and awaited to terminal before another connection
operation; there is no detached timeout task or hard disposal-duration claim. Unknown
transaction acquisition or release acknowledgment quarantines the exact connection's
pool and closes the owned backend rather than reusing uncertain session exclusions.
Sequential terminal close/balancing attempts can occur; no two are in flight together.
Physical release and provider behavior remain runtime proof obligations.

`ProjectSourceMutationScope` records commit attempted before the provider call and
acknowledged only after that call returns. Source release is attempted once across
commit/disposal, preserving both primary and cleanup errors. `DeleteCoreAsync` disposes
scope and context before independent activity or participant completion. Known SQL
acknowledgment survives source/context/publication failure as the existing
`ProjectDeletionPartialCommitException`; lost acknowledgment retains the original
uncertainty and diagnostics. Later concurrency exceptions never prove rollback.

The additive public recovery field `OwnerCompletionUnconfirmed` defaults to false.
True preserves real pending participant IDs, skips independent callbacks and requires
operator review before retry. Empty preparations remain empty. The failure collection
is copied into immutable storage. The declared HTTP 409 response is
`ProjectDeletionCleanupPendingApiResponse(ProjectDeletionRecovery Recovery)` and both
mapped deletion/retry endpoints declare that response type. The new property is on
that public model, not an anonymous payload. OpenAPI generation and downstream client
regeneration/compatibility are integration gates; no SPA cutover is claimed.

Workbench compensation keeps the existing mapped failure and adds optional typed
`CompensationDeletion` evidence: creation happened, compensating SQL deletion committed,
the requested transfer did not complete, and no automatic retry is authorized. CRM's
two existing compensation catches retain the typed deletion exception directly or in
their original aggregate, and continue to require observation; their unchanged source
does not claim the deleted project was retained or the CRM conversion rolled back.

The native subset `F8750903` independently retains exact blocked targets across a
missing/refreshed pending inventory. Both host and renderer guard queued retries; no
read clears local owner-release uncertainty. Confirmed-release ordinary participant
retry remains available. The peer reviewed that subset without running it.

## Source-derived proof selection

All counts below come from actual Fact/InlineData attributes, not runner discovery.
The selected methods have no skips, dynamic providers or excluded-category rows.
HostPlatform attributes remain eligible under the standard Stable filter.

| Selection | Discovery / execution | Source change |
| --- | ---: | --- |
| `ProjectDeletionIntegrationTests` three partials | 30 / 30 | Existing 20 plus 10 new Facts |
| `ResourceTestLabAdmissionIntegrationTests` | 10 / 10 | Existing delayed Resource/Search case strengthened |
| `ProjectsApiDeletionIntegrationTests` | 2 / 2 | Existing actual HTTP case extended |
| `ProjectStructureSubprojectTransferCoordinatorTests` | 7 / 7 | Existing compensation case extended |
| `ProjectDefinitionOperationTests` three partials | 23 / 23 | Unchanged shared-scope regression |
| `ProcessCatalogAuthorityPersistenceTests.ProjectWriters` exact methods | 21 / 21 | Unchanged source authority/commit regression |
| Same class, `Compensation` exact methods | 7 / 7 | Unchanged owner compensation regression |
| Same class, `OpportunityCompensation` exact methods | 7 / 7 | Unchanged CRM compensation regression |
| Native state / renderer / exact normal host retry | 2 + 8 + 1 | Three new Facts; frozen native subset |
| Stage 1 class / exact import-commit methods | 8 + 6 | Separate stage 1 inventory |
| **Disjoint combined selection** | **132 / 132** | **14 new Facts across stages 1/2/native** |

The ten new owner Facts cover same backend and transaction-only exclusions; fresh
post-wait data; lifecycle-before-P ordering; activation change in the gap; actual
table/row NOWAIT refusal; partial-key cancellation; lost lock/unlock acknowledgment;
actual transaction begin/rollback fault points; final source/current-owner checks;
and actual acknowledged/lost-ack commit followed by failing disposal. The last also
holds source release after real compensation commit, proves the call stays pending,
then checks one failed release and exact retained obligations. Disposal errors are
retained alongside the actual lost-ack error.

The Resource causal case observes the exact deletion backend waiting on the Resource
holder, then requires Resource Accepted followed by successful deletion, exact lifetime
retirement and absence of every Project Search row before recreation. It cannot pass
by converting the valid competitor into a contention refusal. The HTTP case separately
proves normal partial cleanup with false permits exact retry, whereas actual owner
disposal failure gives true with exact IDs, acknowledged deletion and no completion
callback. The compensation Unit case retains both the old generic failure and the
new known-deletion evidence without adding a row.

## Transfer and remaining proof

The backend inventory preserves all main/managed predecessor hashes and exact source
backups. `ResourceTestLabAdmissionIntegrationTests.cs` is the one layered dependency:
main `71D0BE01` -> frozen Resource original-state fixture `B5E14880` -> stage 2
`ACA18813`. Transfer the reviewed Resource union first or stage both source layers
before compiling. It uses the original-state Resource commands but leaves its frozen
exclusive project-lock implementation unchanged. The native eight-entry inventory is
disjoint; apply its two rendering-register rows only, never the whole managed register.

Runtime peer rereview passed the cleanup correction at helper `01401B70` and scope
`228148F1`: the waiter-only timeout race is removed, unknown acquisition selects
quarantine, source release cannot repeat, and cleanup diagnostics remain. This is a
bounded source verdict, not proof of physical session cleanup or overall compilation.

Parent-owned standard Release builds, exact discovery/execution, generated response
schema review, full portability/static and renderer gates remain required. No build,
test, database, scan or process was run here. The five-second admission bound is a new
explicit behavior and may refuse prolonged contention; no capacity or throughput
qualification follows from source review. Resource revision migration, retained-data
compatibility, mixed-writer/native/browser proof and 1/8/32-writer capacity remain open.
