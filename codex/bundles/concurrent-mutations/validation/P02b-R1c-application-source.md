# R1c application and receipt-recovery source checkpoint

Status: authored source, not built, discovered or executed for this slice. Parent owns
serialized standard Release builds, exact discovery/execution and the complete static
gate. This document does not promote previous R1a/R1b results into R1c proof.

The implementation follows [the R1c card](../plan/P02b-R1c-application-reconciliation.md)
and the R1bc bounded owner contract. It branches in the existing execution service
before attachment restoration, provider/SDK access or approval mutation. The cohesive
application helper resolves current read from the exact admitted interactive authority
or the real background owner policy. Current read is separate from current write.
No new execution-service partial, endpoint, provider adapter or identity registry was
introduced. Existing producer factories and unsupported-effect guards remain gated.

## Knowledge and lifecycle

Normal execution constructors still require actual metrics, and normal chat requires
an actual assistant message. Only the typed effect-continuation factories permit
absent provider output. Successful receipt-only recovery means a confirmed Waiting
checkpoint; it does not mean task completion, new usage, assistant text, approval, or
permission to retry an effect. The original run, proposal identities, metrics, usage,
SDK Result/DisclosureEvidence and prepared source are preserved.

Checkpoint conflict returns bounded authorized observations with NotAttempted; lost
acknowledgement returns Unconfirmed. Neither reports a successful Waiting transition.
Current-read denial or unavailability discloses no newly observed run-bound knowledge.
The application adds no owner lookup to the side-effect-free GET summary. Its
SnapshotRead/Confirmed result describes an observed stored snapshot, not a publication.
Both execution/chat effect-result constructors, activity suspension/finalization and the
confirmed-checkpoint cleanup carrier reject SnapshotRead. GET and native readback still
accept it as an observation; they cannot manufacture WaitingOnTool from a failed or
cancelled original run.

The cleanup extension disposes the actual coordination lease before final activity.
A failure after an already confirmed checkpoint becomes
AgentEffectReconciliationCleanupException with the same confirmed sanitized carrier;
its private inner exception is not sent over HTTP. HTTP503 uses the distinct
agents.effect-lease-release-unconfirmed code. Successful activity publication reports
Waiting/needs-attention, and Process refuses an automatic replacement. Native retains a separate sticky release
uncertainty and disables another slice. The file lease only closes its stream; it
cannot undo persisted receipt confirmation. An incomplete checkpoint plus cleanup
failure retains the exact original continuation in the typed incomplete exception,
with an immutable release-unconfirmed flag and both diagnostics in its private inner
aggregate. It cannot fall through Process transient classification. Denied, unavailable,
cancelled, admission or other failures plus cleanup use a separate typed lease exception
with a closed original failure kind and no continuation. API and native retain cleanup
uncertainty without inventing receipt knowledge; original cancellation remains Cancelled
in activity. Process requires review without a replacement dispatch. The same handling
covers cleanup after interrupted-lease admission fails before returning a lease,
including real admission-write acknowledgement loss. This acquisition catch is an
explicit production delta from the frozen R1b candidate. There is no new cleanup
command or speculative release claim.

The tests decorate only the existing store's acquired lease, await its real close,
then inject a TimeoutException. Independent bounded reacquisition verifies the physical test
resource was released; production classification remains conservative because a real
close failure need not establish that fact. No production fault hook was added.

The activity amendment moves publication out of receipt reconciliation into the existing
activity boundary. The Core workspace facade, current-profile wrapper and native recovery
orchestrator then explicitly finalize and dispose their owned activity once. Existing
`AgentExecutionActivityPublicationException` carries the exact authorized continuation,
original failure kind, independent file-lease uncertainty and activity-disposal failure;
private aggregate diagnostics retain every original and observational failure. It never
turns confirmed knowledge into Unconfirmed, or activity failure into file-lease failure.
HTTP/SSE expose only a sanitized activity-finalization flag. Native keeps that flag
sticky across SnapshotRead and validates the exception against the actual recovery attempt
stream while preserving the original run-recovery target. No publication or disposal failure
authorizes another effect or automatic Process replacement.

The public Core finalization boundary requires an existing typed effect result/failure;
ordinary provider results and unmarked admission errors are refused before activity access.
Only admission refusal inside the validated effect-recovery branch receives an internal
marker, retaining the public exception type, original code and original inner exception.
Ordinary provider activity paths keep their existing behavior. The historical 49-file
inventory remains unchanged; the amendment inventory records current managed inputs and
their exact main-base hashes, excluding the separate parked JsonStore optimization.

## Exact source-derived rows

Every new case is a Fact; no InlineData, MemberData or dynamic expansion is added.
Loops within a Fact are explicitly asserted arrangements and do not increase rows.

| Assembly | Exact selection | New rows |
| --- | --- | ---: |
| Unit | FullyQualifiedName~AgentEffectContinuationResultTests | 4 |
| Unit | FullyQualifiedName~AgentEffectContinuationConsumerTests | 5 |
| Integration | FullyQualifiedName~AgentEffectRecoveryApplicationTests | 9 |
| Integration | FullyQualifiedName~AgentEffectRecoveryApiTests | 5 |
| Integration | FullyQualifiedName~AgentEffectRecoveryActivityFailureTests | 2 |
| Integration | FullyQualifiedName=CanDoItAll.Tests.Integration.Processes.ProcessCatalogAuthorityPersistenceTests.Expired_process_dispatch_can_read_original_receipt_through_application_without_new_effect | 1 |
| Components | Four new method selections in AgentChatPanelResponsivenessTests, listed by the native proof | 4 |
| **Total** | **9 Unit + 17 Integration + 4 Components** | **30** |

The source inventory records every exact new method and owning file. Existing affected
regressions and their source-derived counts are separate. The new real Process Fact
uses the existing PostgreSQL fixture, actually commits one protected tool effect,
expires its source claim/revokes write, then calls the application reconciliation
helper with the real Process receipt reader. It requires zero GUID/full-run source
reads, original evidence observations, one retained receipt and no second effect;
current read revocation subsequently denies the side-effect-free summary. It does
not claim to activate a real Process effect-only producer or exact saved dispatcher.

The nine application Facts use the actual file store and workspace service with
throw-on-use provider/SDK ports. They cover missing transient input, original authority
binding, current-read revocation, confirmed summary without owner detail reread,
checkpoint conflict/acknowledgement loss, combined checkpoint-plus-cleanup failure,
confirmed cleanup failure, denial/read-outage/OCE plus cleanup, rejected admission and
admission-acknowledgement loss plus cleanup, prepared no-op and dispatch refusal, separate cancellation, and
startup preservation including zero segments. The HTTP fixture forwards calls to that
actual workspace; real API middleware and current-profile activity reader remain.
The host's resolved profile/fingerprint/generation bind the fixture and coordinator.

There is no recovery-command SSE POST. The five routed HTTP Facts exercise existing
REST recovery and GET activity streaming, sanitized GET, typed403/409/503 outcomes,
and a disconnected HTTP caller after the actual durable file checkpoint. The latter
pauses only acknowledgement, always releases its owned barrier, waits for the actual
handler to exit, and verifies confirmation without metrics, messages or task-success
activity. No protocol outcome or commit is fabricated by the HTTP forwarder.

The two activity Facts run actual file-owner recovery through the Core and current-profile
owning facades. Faults arm during real receipt/current-read work, after genuine checkpoint
acknowledgement loss, or after a typed effect-local refusal. They cover Report, Suspend,
IsTerminal, Fail/Cancel and post-real activity disposal, with and without file-lease failure.
Fresh reads preserve exact recorded confirmation and independent physical lease reacquisition
proves the test resource was released. The current-profile synchronous-catch arrangements
inject a typed refusal at its execution-service interface solely to test ownership; they
do not claim a real permission decision or effect. Existing consumer/native/API Facts add
actual native-orchestrator ownership, sanitized response and original-versus-attempt stream
arrangements without adding rows. No activity failure is represented as successful
Waiting publication when the stream could not be finalized.

See [consumer proof](R1c-consumer-source.md) and
[native proof](P02b-R1c-native-source.md) for their exact fixtures and limits. The native
helper's current-read snapshot does not clear release uncertainty or manufacture a
stream for reopened runs. The final combined rendering-register hash supersedes the
older whole-register hash in the native-only inventory; its source rows remain exact.

## Review, compatibility and open gates

Schemas1–4 retain their existing paths. Schema5 only adds the already reviewed
receipt-only recovery path; provider restart semantics and saved SDK state are not
fabricated. AwaitingContinuation is appended as activity17 and as the new floating
state; older enum values remain unchanged. The response adds an omitted-when-null
continuation and otherwise retains normal result JSON. Existing normal constructor
call sites and source metric dereferences were inspected; execution proof is pending.

The direct Core slice has four full run-record reads (two at admission,
two at the conditional checkpoint); the application adds its initial run read, five
on the initialized healthy path. Current pinned Process evidence adds no full-run
read. GET is a separate operation with its own one full detail load. R1b's first
maximum-journal cost execution is characterization, not performance qualification:
there is no invented allocation or latency SLA. Structural budgets remain four
lookups,16KiB response, five-second cooperative slice and independent two-second
checkpoint, with actual OS/file work not described as hard bounded wall time.

Still open: parent build/test/static proof; R1d exact saved-effect dispatch and second
approval-round execution; R2 producer cutover and detached authority; native trusted
reopen-to-recovery admission; explicit operational cleanup/restart follow-up; cancellation
callback migration; P04/P07 snapshot/refresh/pause/fail/replan runtime activation;
remaining legacy producers. This source checkpoint is not end-to-end completion.
