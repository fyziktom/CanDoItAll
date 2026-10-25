# P03: durable lifecycle outcome and deletion completion

Status: decision proposal, source reviewed on 2026-10-17. No implementation, migration,
discovery or execution in this card. It refines [the original checkpoint plan](P03-durable-lifecycle-checkpoints.md)
against C2 finite operations, C3 receipt recovery and the frozen Resource stage-2 deletion
handoff. Stage 2 still needs its actual runtime proof; Resource shared-project stage 3
remains held. The existing P03 test totals are historical, not a current selection.

## Decision

Reuse the neutral finite-operation implementation and the existing Projects pool for
four typed lifecycle intents: delete original project, ensure link, remove original
edge and reconnect original edge. Add one Projects lifecycle receipt family; do not
put deletion into definition receipts or add a second epoch service/worker. The old
checkpoint reference to `ProjectOperationEpochService` is superseded by
`FiniteOperationEpochStore`, `IFiniteOperationReceiptRetentionParticipant` and the
existing composition maintenance scheduler.

An accepted deletion receipt does **not** authorize participant execution. Add a small
Projects-owned durable deletion completion record, committed with the deletion and its
participant preparations. Its release state starts `AwaitingOwnerRelease`; only a
positive owner release checkpoint makes it `Released`. Every actual DeleteProject
cleanup path must observe this gate before claiming work or invoking dependencies.
Keep the existing Workbench and Agent participant journals, claims, heartbeats,
warnings and idempotency. Do not duplicate their task states in Projects.

This separate record is necessary: finite receipts can expire and compensation has an
existing creation-receipt identity, not an ordinary user deletion command. Neither
expiry nor a public operation UUID may govern the lifetime of unfinished cleanup.
Do not add operational release state to `ProjectRetirementRecord`: retirements are
exported as historical business binding evidence, whereas completion authority must
remain local to its original profile/namespace. Two narrowly owned new tables are
proposed, not a general saga, callback registry or durable host registry.

## What the current source proves, and does not

Paths below are relative to the repository root.

| Current seam | Consequence |
| --- | --- |
| `Projects/ProjectModels.cs::DeleteCoreAsync` | Preparations, retirement, root/incident-edge deletion, enlisted Search and routing commit together. Stage 2 awaits source/scope/context cleanup before its initial participant loop, preserving acknowledged commit on cleanup failure. |
| `ProjectModels.cs::RetryDeletionCleanupAsync` | A different request currently scans pending/notices and calls `CompleteAsync` without release evidence. The page-local sticky hold cannot protect this route or another client. |
| `Workbench/CrossModule/ProjectWorkbenchDeletionParticipant.cs::CompleteCoreAsync` | Dependencies execute before the main mutation processor. A gate only around the Projects initial loop is insufficient. |
| `Workbench/CrossModule/ProjectCrossModuleMutationProcessor.cs::ProcessWithPayloadAsync`, `DeleteProjectAsync` | Existing durable claims prevent duplicate owners, but do not prove the deleting owner's release. The DeleteProject dispatch branch must enforce the gate even when reached from another processor caller. |
| `AgentFramework/Persistence/AgentProjectStructureAccessDeletionParticipant.cs::CompleteCoreAsync` | Loads, claims and renews a revocation before calling the file workspace. No Projects release check exists. |
| `MAF/Common/...Persistence/Storage/FileSandboxWorkspaceStore.CatalogReadLease.cs` | Holds the workspace semaphore and cross-process lock. Dispose clears a local reference and then releases resources. The public lease has no durable release witness; lease expiry or a new process is not proof of its successful release. |
| `Projects/ProjectDefinitionReceiptReader.cs`, Workbench task-plan reader/publisher | Reuse coherent observation and commit-knowledge lessons, not target-existence policy: an accepted deletion must remain readable after its target disappears. Initial unreadable/unknown receipt state cannot prove no effect. |
| `Projects/ProjectFiniteOperationPool.cs` | Currently registers only the definition family. Adding a family requires maintenance, offline installation and old-binary compatibility changes before any lifecycle writer is enabled. |

The module abbreviations in the table are under `src/Modules/CanDoItAll.Modules.*`.
The stage-2 behavior and its limitation are documented in
[project deletion completion](../../../../docs/architecture/concurrent-mutations/09-project-deletion-completion.md).

## Immutable intent, receipt and durable release

Retain the original P03 expectations: delete binds original dataset/profile, project
lifetime and scalar definition revision; ensure binds endpoint lifetimes; remove and
reconnect additionally bind the existing edge Guid. Reconnect returns the preserved
or newly created destination edge Guid. Scalar edits alone do not invalidate hierarchy
intent. Creation compensation keeps its original reservation/lifetime/fingerprint and
compensation guard; never manufacture an ordinary delete revision for it.

`ProjectLifecycleReceiptRecord` has the existing typed namespace/epoch/caller partition
and operation UUID address, independent lifecycle digest/result versions and bounded
immutable accepted bytes. No FK to live projects, neutral epoch metadata or participant
tables, and no cascade. Exact replay performs no preparation, mutation, activity or
participant completion. Current absence, an unavailable payload, unsupported version,
revoked disclosure, missing metadata or a later rejection must not resolve an earlier
uncertain attempt as no-effect. Keep initial `Unavailable`/`DetailUnavailable` typed
Unconfirmed, as corrected for C3; a genuine future epoch remains InvalidContext.

`ProjectDeletionCompletionRecord` is keyed by original namespace and completion Guid,
with a unique original project lifetime in that namespace. It records profile/project/
lifetime, origin (ordinary deletion or creation compensation), creation time, version,
the immutable exact participant/recovery references and release state/time. No names,
credentials, serialized source lease, captured service or participant error payloads.
The accepted deletion contains this completion identity and the prepared references;
it certifies `DeletionCommitted`, not cleanup success. A compensation writes the same
completion record in its existing SQL transaction without acquiring a public lifecycle
operation identity. Its origin retains the exact bounded creation fingerprint and
reservation identity where present. A later compensation observation of this exact
binding must report the committed compensation, not infer "project retained" from a
missing live root. This remains a server-owned creation-evidence path with current
disclosure checks, not a new public tokenless deletion API.
Missing or mismatched gate evidence is Unavailable/NeedsReview,
never permission to run and never proof that deletion failed.

Use a bounded initial registration: at most 16 participants, participant ID at most 64
UTF-8 bytes, and at most 16 KiB each for accepted lifecycle bytes and completion
references. These are proposed engineering bounds, to be checked against fully escaped
worst-case codecs and retained compatibility before release. Validate registration and
immutable input before Prepare; recheck exact returned project/recovery identities
before commit. Today the two registered IDs are `workbench` and
`agent-project-structure-access`. Keep the limits owner versioned and typed.

Release is a monotonic, owner-internal CAS, not a client-set boolean:

1. In the original transaction, insert `AwaitingOwnerRelease`, prepare exact participant
   references, perform deletion and insert the immutable lifecycle acknowledgement.
2. Record attempted versus acknowledged commit outside all disposal paths. Await every
   owned disposal to terminal; never timeout only the waiter, retry disposal concurrently
   or start an independent read while still holding SQL/source ownership.
3. Only after all original owner/source release is positively established, use a fresh
   short Projects transaction to CAS that exact completion to `Released`, checking the
   original active binding and immutable identity. This transaction holds no source
   lease, P/hierarchy/B lock or external callback. Its own cleanup cannot retroactively
   invalidate the already-established original release; lost CAS acknowledgement is
   resolved by a fresh exact gate read.
4. Participant dispatch requires `Released` plus current cleanup authority and exact
   original binding, then uses the participant's existing claim. Do not hold the
   Projects read transaction/connection across a participant, file or provider await.
   Because release never reverses, this check does not need a new distributed lease.

A known accepted result remains accepted if release persistence, activity or participant
completion fails. Its separately read completion status remains pending/unavailable.
Commit-attempt uncertainty requires an authorized exact receipt observation after owner
unwind; any cleanup exception, including a conflict-shaped exception, is not rollback
evidence. A duplicate observed under owner exclusion must release both SQL ownership
and the held source/catalog lease before independent disclosure, including queued-writer
cases. If release itself is unconfirmed, skip independent disclosure that could acquire
the same source again; retain known Accepted or Unconfirmed. An exact receipt Found after
lost commit acknowledgement may permit the release CAS only when this same owner attempt
also positively completed its original cleanup. Do not reproduce the C3 cleanup-conflict
or initial-lookup classification defects.

## Restart and multiple actors: fail closed without a fabricated lease

Fresh clients read durable Accepted and the independent gate. `AwaitingOwnerRelease`
blocks **all** participant dispatch, including another host, even when a pending list
says retryable or omits the recovery. A crash between release and its CAS leaves a
conservative hold. Timeout, participant claim expiry, PID disappearance by itself,
another catalog revision, successful current-read authorization or receipt replay does
not clear it.

There is no existing durable source-release protocol to support automatic online
promotion of that hold. The minimum first recovery path is an explicit stopped-host
owner reconciliation: verify the original profile/namespace, exact committed deletion
and participant bindings, and establish quiescence of every original database/file
source owner before writing Released. This is a separately reviewed operational command,
not startup auto-healing or an HTTP override. Database maintenance exclusion alone does
not prove a file/catalog lease released; a catalog lock alone does not prove all SQL
owners stopped. Existing infrastructure does not supply a global host-termination proof.
The exact supported-host quiescence procedure is a release prerequisite. If automatic
online restart recovery is required, design a concrete source-owner fence before coding
that path; do not hide the gap behind a generic `IsReleased` callback.

Legacy unfinished participant records have no such gate. Completed notices remain
readable, but missing legacy gates cannot silently default to Released. The stopped-host
cutover must inventory exact pending work through each participant's owner boundary and
install a reviewed legacy completion binding or refuse activation for ambiguous/unbound
records. Preserve all payloads and existing recovery IDs. Business profile/package
transfer must not copy operational release permission or reconstruct it from imported
retirements. This is a compatibility gate, not permission to discard old pending work.

## SQL ordering, authority and ownership

For the new ordinary lifecycle owner, reuse the proven stage-2 fresh Serializable entry
after its runtime gate: source authority before SQL; lifecycle first; complete ordinal
session key set (operation-domain key, target/endpoints, global hierarchy and actual
participant preparation keys); same backend handoff to fresh Serializable; utility
lifecycle NOWAIT before the first snapshot; checked transaction-lock handoff and reverse
session release before effects. Include the operation key before the first snapshot so
a waited duplicate is actually visible. Reuse the existing helper by a narrow lifecycle
name when both callers need it; do not copy its cleanup machinery or add strategy flags.

Enter the original Projects epoch and recheck finite admission inside that protected
owner transaction. Epoch maintenance takes no P/hierarchy/B keys and uses nonwaiting
row selection; retain that property. Stage-1 fixed import lifecycle NOWAIT is mandatory
for the transitive B/import/lifecycle/P graph. No global Serializable-to-RC change.
Keep full hierarchy cycle validation under the global hierarchy exclusion. A coherent
opening supplies bounded selected endpoints/edge facts; independently loaded display
lists do not become the original witness.

Current write authority remains host-neutral. Reuse the existing current-policy
observation implementation for HTTP through an explicit lifecycle adapter; do not treat
the original expectation or local release state as permission. Before deletion effects,
validate root CAS/lifetime and all live sources; at final commit, revalidate the actual
live sources and other endpoints without demanding that the deliberately deleted root
still exists. Minimal human deletion disclosure requires original caller partition,
current Projects read and original active namespace, not a live deleted target. Cleanup
write/retry authority is separate. Governed hierarchy needs the admitted original source
and current disclosure policy; no Projects dependency on Web, Workspace or Agent Core.

Expose a narrow Projects completion query to existing dependent participants, using
typed completion/participant/recovery references and bounded exact reads. Enforce it
before Workbench dependencies and again at the DeleteProject processor dispatch seam;
enforce it before Agent revocation claim/dispatch. Keep Projects free of participant
DbContexts. Composition supplies the existing participant list; there is no second
registry. Reconciliation status reads must use keyed participant observations rather
than full pending/completion lists. The participant remains responsible for validating
its own original namespace/profile/lifetime and current authority across awaited work.
Replace the participant preparation's bare project ID with one immutable typed context
carrying that original binding and owner completion identity. Persist it in the existing
participant record/payload before returning preparation. The gate query uses its exact
indexed namespace/completion address and validates the selected participant/recovery
tuple within the bounded references; no scan of all pending records and no claim that
an unrelated recovery for the same project proves release.

## Complete writer and caller perimeter

| Family | Concrete production paths to adapt or prove |
| --- | --- |
| Ordinary deletion | `Projects/ProjectModels.cs::DeleteAsync/DeleteCoreAsync`; native `Projects/Pages/ProjectsPage.razor`; `Web/Api/ProjectsApi.cs` delete and cleanup routes. Remove tokenless public deletion after caller cutover; missing root is not an orphan-cleanup request. |
| Creation compensation | `Projects/ProjectCreationReceipt.cs::TryCompensateCreationAsync`; `Workbench/ProjectStructure/ProjectStructureSubprojectTransferCoordinator.cs`; `CrmHr/Services/CrmHrServices.cs` opportunity conversion/rollback callers. Preserve their creation evidence, give every committed compensating deletion the same release gate, and retain typed committed-vs-retained reporting. |
| Participant completion | `Projects::RetryDeletionCleanupAsync`; Workbench `ProjectWorkbenchDeletionParticipant` including `CompleteDependenciesAsync`, `ProjectCrossModuleMutationProcessor::DeleteProjectAsync` and caller `ProjectWorkbenchCrossModuleMutationService`; Agent `AgentProjectStructureAccessDeletionParticipant`. Cover direct processor entry and both old/new clients; no background processor may bypass the gate. No autonomous deletion-completion worker was identified in this source pass. |
| Hierarchy intents | `Projects::AddSubprojectAsync/RemoveSubprojectAsync/ReconnectSubprojectAsync`; `Workbench/Pages/ProjectStructurePage.ProjectHierarchy.cs`; Projects API add/remove/reconnect; `Workbench/ProjectStructure/ProjectStructureAgentService::ChangeSubprojectAsync`; `Web/ProjectStructureAgentApi::ChangeSubprojectAsync`; `Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider` subproject-change path. The tool currently captures endpoint admissions at dispatch; add the original edge witness and durable admitted operation identity instead of refreshing it there. |
| Other edge writers | `ProjectDefinitionWriter` child creation; `DeleteCoreAsync` incident-edge removal; `ProjectsProfileTransferStore::ClearAsync/SaveAsync` hierarchy replacement/import. Keep their own existing transaction/identity semantics, complete hierarchy locks and retained edge IDs; do not create synthetic user hierarchy receipts for import or child creation. |
| Transfer/retention/composition | `ProjectTransferTargetStateParticipant`, `ProjectTransferReferenceQuery`, `ProjectsProfileTransferStore`; `ProjectsDbContext` and canonical model; `ProjectFiniteOperationPool`, Projects DI and offline activation registration; new lifecycle retention participant. Both new tables are explicit target residue, absent from business DTO payloads, and excluded from ordinary business-change telemetry where current receipt conventions require it. |

File prefixes in this table are the module and Web roots used above. The prospective
new owner files are `ProjectLifecycleContracts.cs` in Projects.Contracts and Projects
`ProjectLifecycleOpening.cs`, `ProjectLifecycleOperationCodec.cs`,
`ProjectLifecycleReceiptRecords.cs`, `ProjectLifecycleReceiptReader.cs`,
`ProjectLifecycleReceiptRetention.cs`, `ProjectLifecycleWriter.cs`, and
`ProjectDeletionCompletion.cs`. Keep concrete deletion and hierarchy functions small
inside that owner slice; do not create a generic effect-delegate coordinator.

## Retention and migration boundary

Add lifecycle to the existing Projects pool and reuse its aggregate 128-row/16-MiB,
two-second maintenance cycle and family fairness. Do not double budgets. Bump the
Projects retention protocol from 1 to 2 in the installed registration and supported
retained Projects state, preserving every epoch number/time/issued promise and every
definition receipt byte. Definition request/result version 1 is unchanged. The existing
exact installed-pool fence does not detect a new family by itself: without this version
cutover an older binary could ignore lifecycle rows during metadata deletion. Workbench
pool/version and independent receipt payloads remain unchanged. Require exact catalog,
participant registration and stopped-host migration proof; missing metadata must not
be initialized by ordinary readiness.

Completion records are not finite-operation receipts and never disappear merely because
an epoch retires. Keep unfinished/unreleased records. A later bounded Projects-owned
cleanup may remove a Released record only after exact bounded participant observations
prove every listed obligation terminal and its finite receipt horizon has closed; missing
participant evidence is not terminal. Initial implementation must state its retained
completion storage growth and alerting budget, not imply the neutral purge service bounds
it. No cross-owner FK/cascade or simultaneous multi-owner table locking for cleanup.

## Proof and performance release gates

Author actual cases before counting them; no new test count is claimed here. Reuse the
existing isolated PostgreSQL TestApplication/lease and relevant classes: deletion
integration (including FreshSnapshot), API deletion, shared-storage deletion, lifecycle
coordination, hierarchy owner/Components, compensation/CRM opportunity, finite retention,
transfer target, and native pending storage/restart/browser suites.

- Real commit plus lost acknowledgement/cleanup conflict returns or later recovers exact
  Accepted. A same-envelope outage/refusal never changes prior uncertainty. Replay after
  project deletion/recreation or edge edits does not invoke mutation/preparation/activity.
- Hold actual source or context disposal after SQL commit. A second client/host and direct
  participant/processor calls cannot dispatch dependencies, file revocation or storage
  cleanup. Release CAS permits the existing exact claim path; simultaneous retries still
  have one participant claimant. Crash before/after CAS and lost CAS acknowledgement keep
  correct knowledge. Test supported stopped-host recovery, not an elapsed-time release.
- Stale delete revision refuses before every Prepare/Search/routing effect. Remove/recreate
  ABA survives stale remove/reconnect; ensure no-op is receipted; preserved destination
  edge identity, endpoint recreation, source revocation and joint cycles are tested.
- Reader final binding/current read checks survive awaited authority changes. Duplicate
  disclosure with a queued catalog writer begins only after both source and SQL release.
  No observation invokes activation, receipt writes or participant work.
- Migration preserves definition receipts, Projects/Workbench epochs and legacy participant
  bytes; protocol-1 binary/readiness refuses; new family prevents premature metadata purge;
  unfinished completion survives receipt expiry; imported history cannot grant cleanup.
- Native lifecycle pending storage is additive/family-separated and preserves old definition
  records. Prepared/Dispatching/Accepted CAS survives restart and two actors; acceptance
  remains sticky and server gate status, rather than a page flag, governs cleanup.

Measure acceptance and completion separately. Use 1/8/32 actors, disjoint vs same target,
small/retained hierarchy edge counts and small/large owned object/storage sets; record
all-context SQL count, lock wait/hold, exact outcomes, allocations (process totals for
concurrent work), and p95/p99 only with a declared sample regime. Replay/gate observations
must be exact indexed reads plus current authority, independent of graph or pending-list
size. Whole graph traversal and delete preparation remain proportional to retained data;
small 16-KiB commands do not bound that cost. No throughput/SLA claim follows from C3's
54 samples or from stage-2 fault correctness.

Release order: stage-1/stage-2 actual proof; review this decision and stopped-host recovery
contract; dormant owner contracts/schema/retention/gate; complete ordinary-delete and
compensation/participant cutover; hierarchy and governed caller cutover when runtime
operation identity is ready; full static/build, selected PostgreSQL faults/barriers,
HTTP/native/restart/browser and measured tails. P03 and Resource stage 3 are not released
by this document. Automatic online release reconciliation and legacy cutover are explicit
design gates, not presumed capabilities of the existing finite-operation framework.
