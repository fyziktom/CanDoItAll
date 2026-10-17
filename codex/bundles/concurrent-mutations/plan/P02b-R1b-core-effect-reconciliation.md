# R1b: bounded Core effect-journal checkpoint

Status: the parent confirmed R1a's 41 Unit + 50 Integration rows passing and the
15,454-finding final no-write static gate, released R1b-first, then explicitly
released the remaining R1b/R1c source work after review. The complete R1b candidate
and its 24 rows are now source-authored in the managed checkout, **unbuilt and
unexecuted**. R1c application consumers are not yet connected. This card refines R1b in
[the ordered runtime plan](P02b-runtime-journal-schema.md); it does not activate an
Agent, Process, detached or MAF producer. R2a/R2b/R1c/R1d and legacy cutover remain open.

[The bounded reconciliation amendment](P02b-R1bc-bounded-reconciliation.md) is
authoritative for slice/cursor limits, pinned lease snapshots, conditional batch
publication, Process read observations, compact results and actual store cost.

## Historical first buildable release after R1a

The released first R1b source checkpoint is **Core lease/claim infrastructure only**.
It does not implement the bounded receipt loop or activate any producer.

| File | First-checkpoint change |
| --- | --- |
| `ToolPolicy/AgentToolRunLease.cs` | Closed lease mode; private owner-held immutable admitted run/chat view; one-time pin after successful interrupted lease admission. No snapshot accessor on the public claim. |
| `ToolPolicy/AgentToolReceiptReconciliation.cs` | Optional typed `AgentToolPinnedReceiptEvidence` projection, internally constructed and immutable; transient trusted current-read callback on interrupted claims. Existing cancellation signatures and admission constructor remain. |
| `ToolPolicy/AgentToolAdmissionJournal.cs` | Explicit mode guards for every existing write/dispatch entry point, including the direct `SaveApprovalCheckpointAsync` writer path; preserve ordinary and cancelled behavior. Select pinned verification only for interrupted claims. |
| New `ToolPolicy/AgentToolAdmissionJournal.Effects.cs` | `AcquireInterruptedReconciliationAsync` and private pinned-claim verification. Reuse the real run lease and existing atomic detail update; keep the exact returned detail rather than issuing another read. This is the first portion of the already planned cohesive Effects partial. |

The admitted snapshot pins only the private data needed for identity/claim and later
CAS checks. Copy caller-mutable list views, including run pending approvals, retained
approval records and any retained chat collection, into owner-held immutable values
once. Do not deep-serialize the journal to clone it or pin unrelated log/metric/artifact
collections. The immutable journal arrays/payloads are shared safely. The pin remains
valid only for the real live owner lease; it does not assert that another writer
cannot cancel, decide approvals or change run state while the lease is held.

Use `AgentToolAdmissionJournalFixture`'s existing retained-run restoration seam for
test-only schema-five input before the initial save; leave all production factories
unchanged. Carve these **four rows** from the existing 24-row prospective inventory:

1. `Readonly_lease_rejects_every_dispatch_batch_provider_and_approval_entrypoint`:
   two InlineData rows, cancelled and interrupted.
2. `Interrupted_reconciliation_requires_exact_nonterminal_effect_session`: one Fact
   with finite source/profile/state/schema cases and current-read denial checks.
3. `Independent_journals_serialize_reconciliation_and_preserve_original_intent`:
   one Fact proving independent file-store exclusion, stale/disposed/foreign claims,
   pinned collection alias protection, exact callback observation and zero extra
   run-record reads during repeated pinned claim verification. This may construct
   the internal owner claim from the friend test assembly; it must not claim the
   not-yet-implemented public reconciliation loop was exercised.

Expected first-checkpoint Integration selection is **54 rows** (existing 14 + 34 + 2,
plus these four), with **41 existing Unit rows**, total **95**. The four new rows are
authored in `AgentToolEffectJournalIntegrationTests.cs`: two Facts and one two-row
InlineData theory, with no dynamic data. The exact new filter is
`FullyQualifiedName~AgentToolEffectJournalIntegrationTests`; source-expected discovery
and execution are both **4**. No build, discovery, execution or static result exists
for this subset yet. The remaining 20 planned R1b rows and
the cursor, bounded loop, final CAS, effect segment/approval/provider methods remain
for the next release. The complete planned R1b inventory remains 24 new / 74 selected
Integration rows; splitting the work does not remove any final proof.

Process original-evidence query/Workbench selection is a later explicit prerequisite
before claiming the complete Process recovery path meets the decode budget. These
54 file-only rows cannot prove current PostgreSQL claim/material observations. The
R1c real expired-Process proof must cover that seam, current read revocation, exact
original binding, no repeated run reads and no mutation admission from its result.
No new dynamic test data or uncounted provider/DB execution is hidden in this first
checkpoint.

The independent-store row counts calls made by the journal through its store boundary:
one atomic admission update, zero explicit run reads, and no further update/read during
repeated pinned claims. The proxy forwards to the real file store and real cross-instance
dispatch lease; it changes only the returned collection view to expose aliasing. This is
not an instrumented measurement of the file store's two internal admission decodes or
of later whole-slice physical I/O. The bounded amendment retains that separate proof.
The claim callback is not supplied by a live application producer yet. Its production
current authority adapter, Process original-evidence observer and final conditional
checkpoint remain required before live interrupted recovery is enabled.

## Exact source boundary

Paths below are relative to `src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/`.

| File / seam | Minimal change |
| --- | --- |
| `AgentToolRunLease.cs` | Replace internal reconciliation boolean with closed `Dispatch`, `CancelledReceiptReconciliation`, `InterruptedEffectReconciliation` modes. Existing public acquisition methods retain signatures. A read-only lease cannot authorize an invocation claim, provider request/response, segment, batch or approval publication. |
| `AgentToolAdmissionJournal.cs` | Keep original factory versions/support unchanged. Factor exact lease-mode and run-state checks; add effect segment/approval publication and explicit interrupted-reconciliation acquisition. Preserve independent file-store dispatch lease, active persisted lease ID and exact session/profile/source checks. |
| New `AgentToolAdmissionJournal.Effects.cs` | Own effects-only segment and bounded interrupted receipt slice. This is a cohesive partial of the existing owner, not a new journal/store/service. |
| `AgentToolAdmissionJournal.ProviderDispatch.cs` | Allow a new actual live effect provider request only under the persisted origin dispatch lease. Keep schema 5 rather than assigning schema 3; retain exact native approval/response/claim validation. Completion may record only that original started dispatch and actual response. No provider is called here. |
| `AgentToolReceiptReconciliation.cs` | Carry trusted current-read callback on transient reconciliation claims. Return the one Models-owned immutable `AgentEffectContinuation` carrier directly; do not introduce a parallel Core result. Preserve the existing provider interface and observation pair, and existing cancellation result contract. |
| `AgentToolAdmissionJournal.Background.cs` | Reuse current source `ReadAllowed` observation for reconciliation, without requiring `DispatchAllowed`. Do not add a detached source kind or relax missing Process policy. |

`AgentToolJournalTransitions` and schema models already own the R1a immutable
OwnerReceiptConfirmed contract. Reuse it and add only the optional schema-5 cursor
and its owner-specific transitions specified by the amendment. The pinned snapshot
and Process original-evidence read seam are explicit prerequisites; they do not
change mutation authority. No Projects writer, operation codec, owner receipt
protocol, Web endpoint, SDK adapter or startup recovery change belongs to R1b.

## Concrete entry points

Add these methods on the existing journal, using existing typed inputs:

```csharp
Task<AgentToolJournalRecord> BeginEffectSegmentAsync(
    AgentToolRunLease lease, CancellationToken cancellationToken);

Task<IReadOnlyList<PendingToolApprovalRecord>> SaveEffectApprovalsAsync(
    AgentToolRunLease lease, Guid segmentId,
    IReadOnlyList<PendingToolApprovalRecord> approvals,
    CancellationToken cancellationToken);

ValueTask<AgentToolRunLease> AcquireInterruptedReconciliationAsync(
    AgentToolSessionReference reference, CancellationToken cancellationToken);

Task<AgentToolEffectReconciliationResult> ReconcileInterruptedAsync(
    AgentToolRunLease lease,
    IReadOnlyList<IAgentToolReceiptReconciliationProvider> providers,
    Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead,
    CancellationToken cancellationToken);
```

The callback is an application-owner dependency, never client JSON or a stored
permission bit. The journal creates the observation from its verified saved session,
governance and turn reference; the callback resolves **current** owner permission
without restoring attachments. R1c will provide its actual authority-resolver adapter.
R1b tests supply a deterministic current owner callback. Do not add a global registry,
resolve UI services in Core, or capture a one-time `true` as runtime authorization.

The Core result contains the original run/chat identity, at most four immutable
sanitized observations, checked snapshot/progress counts, typed stop/persistence
status and `AgentToolContinuationRequirement`. Owner receipts remain inside the
checkpoint operation; this result carries no raw receipt, proposal source, SDK Result
or DisclosureEvidence. This is not `ExecutionRunResult`; R1c owns public mapping.
Previously confirmed proposals contribute sticky KnownCommitted counts without
being looked up again or disclosing cached detail. Current read denial suppresses
new knowledge; failed publication can expose actual observed commitment only with an
explicit nonpersisted flag and currently allowed run read. An outage/miss cannot
erase prior commitment or authorize retry. Detail-unavailable/denied semantics remain
required for R1d's explicit saved-output restoration; ordinary summary recovery does
not claim to have requested that detail.

Keep `ReconcileCancelledAsync(..., bool currentReadAllowed, ...)` and its existing
callers for compatibility in this slice. Its existing owner provider/current
background-read checks remain. The new interrupted path requires the callback;
do not claim the old boolean alone is a new final-current interactive authority
guarantee. R1c can migrate cancelled callers to a callback overload separately.

## Live origin and approval rules

Only schema 5 / EffectJournalOnly accepts BeginEffectSegment. One effects-only segment
belongs to the live dispatch lease that first creates it. Repeating that begin under
the same lease returns the same segment; a replacement lease cannot create a new
segment or append a newly generated model batch. Subsequent live responses share the
same segment and must first drain earlier unresolved effects/provider requests.
The saved segment has EffectsOnly kind, OriginDispatchLeaseId and no restart or
approval checkpoint. Never manufacture an SDK envelope from a receipt or text.

AdmitBatch and provider-dispatch entry points must validate the origin lease for
effect-only support; Recoverable keeps its existing restart behavior. Reject
RequestScopedInput as today. Preserve the append-only provider records and original
request/contract digests. For effect-only native approved calls, the exact saved
pending approval belongs to that same live segment; for Recoverable the existing
previous-segment rule remains. Missing response/uncertain native dispatch cannot be
retried by a replacement lease. R1d must prove the adapter has actual live input
before it ever invokes these live-only methods.

SaveEffectApprovals binds only exact existing pending proposal CallId/ToolName/digest
in the current origin segment. Reuse BindApprovals and the existing atomic run-detail
writer; persist WaitingOnTool, exact PendingApprovals and the typed requirement,
without writing SerializedSessionStateJson or a provider checkpoint. Decisions
themselves still use the existing exact stored approval transition. Read-only leases
cannot publish approvals. Saving provider approvals continues to require a dispatch
lease and a ProviderRestart segment explicitly; its current direct writer path
must not bypass the new mode guard.

## Reconciliation-only state and truth

| Mode | Allowed run / operations | Forbidden behavior |
| --- | --- | --- |
| Dispatch | Existing nonterminal live run; interactive latest-chat check; existing current source dispatch rules. | No dispatch with a read-only lease, no new effect batch under a replacement origin. |
| Cancelled receipt | Exactly RunOutcome.Cancelled; existing no-dispatch cancellation transitions and historical read. | Cannot convert Cancelled to OwnerReceiptConfirmed, clear cancellation or revive the run. |
| Interrupted effect receipt | Schema 5 / EffectJournalOnly, not Completed, no Succeeded/Cancelled outcome; Preparing/Running/WaitingOnTool/Persisting with no terminal outcome, or Failed with null/Failed outcome. Holding the real cross-instance lease proves no live dispatcher owns it. | Cannot call SDK, tools, provider, approval creation or ordinary session disclosure; cannot change current chat identity or synthesize original input. |

Both reconciliation modes retain original agent/profile/generation/source/governance
binding and active persisted lease. An old chat run may be reconciled after another
run becomes latest, but only current owner read authorization can disclose its receipt.
Interrupted acquisition changes only the persisted active lease/revisions. It does
not claim the run was completed, cancelled, provider-restartable or input-restored.

From the lease-admitted immutable snapshot select at most four exact approved
OwnerReceipt mutations with original dispatch claim and Executing/ReconciliationRequired
state, following the durable cursor. Find exactly one provider per candidate. Multiple
owners fail; none yields ReaderUnavailable and leaves uncertainty. Look up outside
the run-file mutation lock, then perform one atomic batch compare against the original
run/journal revision, exact lease/binding/approval/state and proposal evidence. Include
waiting state, requirement and cursor in that checkpoint. No owner DB or network read
is awaited inside the mutation callback; R1c adds no separate summary save.

Invoke currentRead on the verified observation before lookup and again through the
claim before disclosing returned evidence. Existing background sources additionally
use current source ReadAllowed, independent of expired DispatchAllowed. The Projects
provider already calls `claim.RequireAsync` before and after lookup and its receipt
access checks the current target read policy. A fake/misconfigured provider does not
remove the journal's final current-read check. A denied/outage response is never a
write retry authorization.

Found complete bounded owner evidence sets OwnerReceiptConfirmed + Committed, retaining
the original dispatch claim, Result and DisclosureEvidence exactly. Existing Result
may be null or an earlier uncertain SDK result; never create or reinterpret it.
Confirmation is terminal; ClaimInvocation must reject it. Preserve confirmation even
if later disclosure is denied. Subsequent summary recovery reports KnownCommitted
under current run read and performs no automatic owner detail read. R1d must require
current target read and exact equality before restoring an actual original result;
missing/expired/denied/different evidence never permits cached disclosure or another
effect. Lost journal-commit acknowledgement is handled by rereading the same persisted
intent on a later lease; do not allocate a replacement operation.

Owner observation and journal persistence are separate facts. The existing 24 MiB
`AgentToolJournalRecord.MaximumUtf8Bytes` aggregate bound and 8 MiB
`AgentToolProtocolEnvelope.MaximumUtf8Bytes` per-envelope bound still apply after
adding receipt evidence.
An owner-confirmed receipt can exceed the remaining journal capacity; do not truncate
it, evict original intents/results, raise the bound silently or claim a successful
journal write. Preserve the unchanged exact intent and report the persistence failure
with typed, bounded commitment knowledge for that binding (no raw receipt in an error).
If this call already observed acceptance, its failure presentation must not say the
effect was rejected or permit a new operation. A crash before the journal write can
lose that transient observation; later recovery must query the same original owner
operation again. An acknowledged prior OwnerReceiptConfirmed remains immutable even
when a later conditional checkpoint fails. This is not a promise that recovery can always
persist within an exhausted journal bound.

Projects `ProjectDefinitionToolReceiptRecovery.ReconcileAsync` (the approved read-owner
extraction from `ProjectDefinitionToolAdmission`) maps both Open
NotObserved and ClosedAbsent to generic `AgentToolReceiptObservation.NotObserved`.
That is conservative for R1b: leave the proposal uncertain and return
EffectReconciliationRequired. A generic miss does **not** prove not committed, imply
the epoch remains open, or permit retry. No richer absence proof is needed for this
read-only checkpoint. R1d's exact saved-proposal dispatch must independently call the
existing owner RequireOpen gate and cannot dispatch ClosedAbsent, expired/unavailable
or altered context. Operation address, epoch, reservation and original expectation
never refresh during either path.

Prepared/rejected/completed/read-only/non-owner/native-uncertain proposals retain their
existing states. R1b does not execute a prepared approved tool. Uncertain effects or
native provider requests give EffectReconciliationRequired. ProviderInputRequired
requires evidence that actual remaining provider/input work lacks its original input;
it is not the default for every other proposal. In particular an already-approved,
self-contained Prepared proposal has neither invented uncertainty nor missing input:
the receipt-only operation is a no-op for that proposal and infers no requirement
(None unless other actual work supplies one). Pending approval is also independent.
The unimplemented saved-effect dispatcher is a capability gate, not a domain reason;
R1c refuses unsupported dispatch before changing approval decisions, and R1d owns its
activation. The Core result must not erase original Prepared proposals when it returns
an empty receipt-observation set. See [R1c's application mapping](P02b-R1c-application-reconciliation.md).

ProviderInputRequired never permits reconstructing missing tool output, turning owner
evidence into an SDK response or issuing another copy of a confirmed effect. R1c/R1d
retain that distinction even if a caller supplies new input later. Receipt confirmation
alone never means successful task completion. No provider usage metric is fabricated.

## Prospective focused proof and counts

The base table has **18 source cases**; the bounded amendment adds six,
for **24 authored cases** for R1b. `AgentToolEffectJournalIntegrationTests` has
19 Facts and two two-row InlineData theories, **23 file-store rows**. The real
Process case lives in `ProcessCatalogAuthorityPersistenceTests.EffectReconciliation.cs`,
one Fact using the existing owned PostgreSQL fixture. No dynamic data or Skip is added.
No build, discovery or execution is claimed for the complete candidate.
Use the existing real file-store fixture and independent stores/leases. The test-only schema-5 initial
fixture branch before initial save; do not enable a production factory to make tests
pass. No PostgreSQL, container or provider invocation is needed.

| Test name | Planned rows |
| --- | ---: |
| `Effect_origin_segment_is_idempotent_and_only_original_live_lease_can_append` | 1 |
| `Effect_approvals_bind_original_proposal_without_any_sdk_checkpoint` | 1 |
| `Effect_provider_dispatch_preserves_schema_five_and_exact_native_response_fences` | 1 |
| `Readonly_lease_rejects_every_dispatch_batch_provider_and_approval_entrypoint` | 2: cancelled / interrupted |
| `Interrupted_reconciliation_requires_exact_nonterminal_effect_session` | 1: finite invalid state/source matrix inside Fact |
| `Independent_journals_serialize_reconciliation_and_preserve_original_intent` | 1 |
| `Owner_confirmation_preserves_original_result_disclosure_and_dispatch_claim` | 2: prior Result absent / present |
| `Confirmed_receipt_is_terminal_and_summary_never_discloses_cached_detail` | 1: no owner detail lookup; sticky commitment under current run read |
| `Receipt_miss_including_closed_absence_never_authorizes_retry_or_clears_uncertainty` | 1 |
| `Missing_duplicate_malformed_and_failing_receipt_owners_fail_without_invented_acceptance` | 1 |
| `Current_read_revocation_before_or_during_lookup_denies_historical_disclosure` | 1 |
| `Expired_write_source_with_current_read_can_confirm_an_original_effect` | 1 |
| `Reconciliation_cannot_change_original_binding_after_owner_observation` | 1 |
| `Journal_commit_ack_loss_or_capacity_failure_preserves_exact_intent_and_known_commitment` | 1: finite ack-loss / exhausted-capacity matrix inside Fact |
| `Cancelled_reconciliation_preserves_cancelled_disposition_and_never_dispatches` | 1 |
| `Prepared_work_is_a_receipt_noop_while_native_uncertainty_requires_reconciliation` | 1: also distinguishes actual missing provider input and pending approval |

The callback tests must assert it receives the verified session/turn/governance,
not just count calls. The lost-ack case uses the existing file-store commit-boundary
hook and a new store/lease. Verify complete receipt, retained prior fields and unchanged
proposal/operation identity, not merely a success flag. Outage cases propagate explicit
failure while the file remains uncertain; no infrastructure fault becomes NotObserved.

Prospective Integration filter:

```text
FullyQualifiedName~AgentToolEffectJournalIntegrationTests|FullyQualifiedName~AgentToolAdmissionJournalIntegrationTests|FullyQualifiedName~AgentToolBackgroundJournalIntegrationTests|FullyQualifiedName~AgentToolEffectOnlyActivationBoundaryTests|FullyQualifiedName=CanDoItAll.Tests.Integration.Processes.ProcessCatalogAuthorityPersistenceTests.Expired_write_source_with_current_read_can_confirm_an_original_effect
```

Existing source baseline is 14 + 34 + 2 = 50 rows; adding the amended 24 yields a
74-row target. The R1a Unit filter remains 41 rows. These totals must be re-derived
from actual authored attributes before parent discovery. Standard Release build,
focused proof and final portability-static are parent-owned exit gates. R1a's
explicit MAF unsupported guard and unchanged producer factories must remain until
the later checkpoints close; passing Core tests is not end-to-end activation.

## Separate current HTTP authority observation

This paragraph records the original source finding before the separately released
[H2 current-write cutover](P02b-H2-current-write-cutover.md). H2 now owns its source
repair and proof; R1b neither implements nor claims validation of that change.

Source review confirms admission-time write authorization for the current protected
HTTP path. `Web/Api/ProjectDefinitionHttpAccess.ResolveAsync(writing: true)` resolves
the current verified principal and requires Projects read + write, then constructs
`ProjectDefinitionInvocation(caller, receiptAccess)` with null mutation Authorization.
`WebCurrentPrincipalResolver` rechecks managed credential state/subject/expiry and
current allowed scopes when invoked. ReceiptAccess invokes it for current read.
`Projects/ProjectMutationAuthority.CommitAsync` revalidates a source only when an
Authorization supplied one. No HTTP-specific final write check was found after the
project lock. A request already admitted can therefore continue after a managed
write revocation while it waits. Receipt-read permission checks do not prove current
write permission. This is an explicit scope limit, not a new replay vulnerability
or authorization fallback. Workspace W1b must state the same admission-time semantics
or implement a separately reviewed/tested current write fence. R1b makes no HTTP
authority change and must not advertise a stronger guarantee.
