# Initial business binding: implementation decision

This design refines the existing Process producer design before implementing its
business binder. The selected-key capability and its source-lifetime corrections now
pass 54 affected behaviors with frozen source/runtime evidence. This design is accepted
for implementation.

Add an explicit IAgentInheritedPlanningInitialBinder capability to the existing Core
participant contract. It accepts the actual candidate/session, original parent envelope
and typed-owner evidence envelope. Core never references Processes or Workbench. The
existing Workbench participant delegates to one concrete ProjectTaskPlanProcessBinding
service; no additional trivial interface, runtime partial or generic registry is needed.

The coordinator currently indexes inherited participants only by business owner. That
would make a Process and a Workflow adapter for the same task-plan owner collide during
construction. Require SupportsParent on IAgentInheritedPlanningParticipant itself,
including the existing explicit test participant; it has no permissive default. Select
exactly one participant by owner and supported canonical parent in initial runtime
contract capture, current reads and provider entry. An absent or ambiguous match refuses
before provider entry. The optional initializer extends that selected participant with
BindInitialAsync. This prevents the new initial-binding selection from disagreeing with
later recovery/runtime selection. Cover distinct parent contracts sharing an owner,
unsupported parents and ambiguous matches, while retaining ordinary coordinator tests.

The public workspace request must supply a pure CaptureSourceEvidence(candidate)
callback instead of its current caller-supplied business BindAsync callback. Creation
time belongs to the actual prepared candidate, so a precomputed evidence envelope is
insufficient. The callback only translates the owning Process metadata into the bounded
evidence protocol. The coordinator selects the registered participant and invokes its
initial-binding capability. Keep the lower-level run-admission callback for the existing
journal boundary and its direct tests. Recovered workspace children never invoke the
source-evidence callback or initializer. This keeps selection in one owner without a new
registry service, and prevents arbitrary workspace callers from supplying business grants.
Update the explicit workspace test participant to provide its initializer; recovery
tests make the evidence callback throw to prove it is not reused.

The composed validation reuses one SQL fixture for root/step preparation and workspace
execution. It checks both snapshot modes, original material/grant, one provider request,
waiting approval and read-only reopening with substituted input refused. Its explicit
fixture input does not qualify the pending immutable product-input projection.

Processes.Application owns ProcessDecisionInitialSourceCodec for the existing
ProcessExecutionClaimEvidence. Use strict canonical JSON with a version and 4096-byte
UTF-8 bound; valid persisted step keys are bounded at 256 characters, so even JSON
escaping fits. Require nonempty identities, a nonblank step key, UTC nondefault creation
time and dispatch eligibility. Reject unknown/duplicate fields, noncanonical text,
unsupported versions and oversized input. Modules.Processes supplies the evidence using
its existing private claim metadata reader; Workbench must not copy that parser.

The binder checks candidate/session lineage, exact child execution and executor,
Preparing state/revision, no existing admission/chat/outcome, exact UTC creation time,
source kind/id and the Core session's background owner fingerprint. It uses the existing
selected dispatch authority query and its OriginalDecisionPolicy; it cannot obtain
policy from a mutable assignment or reconstruct a grant.

After that selected query, acquire the narrow executor/launcher authority snapshot once,
deduplicating identical identities. Require active executor, CanUseTools and task-write
permission, exact current profile/generation, held launcher source ceiling and original
Project lifetime. Read only the selected original material and child-specific finite
write context. Require dataset namespace and material Project lifetime to match the
source. Check held grants and profile again before returning. Core's final background
dispatch observation remains mandatory; this binder does not hold a Process transaction.

Reuse the existing target codec's source/permission checks: extract a cohesive
RequireTarget(target, original) validation from Target(decision), retaining the
decision-session/time checks in Target. Both initial binding and saved-target reopening
then validate the same target invariants without manufacturing a temporary decision.
Verify canonical material through Basis/Material, not recapture. Let a small typed
ReadBasis result return both the basis and decoded Project material so initial binding
does not decode a potentially large material payload twice.

Keep the existing intentionally historical-invalid fixture path for publication
negative tests. Add an explicit real-initializer option using the registered source.
Positive cases cover operator source, distinct source/executor and the same Agent.
Denial cases exercise source/executor revocation, changed generation/lifetime/namespace,
wrong child/claim/material/grant/session and cancellation; the original saved execution
must remain byte-equivalent. Follow with the real root/step producer and fake-wire MAF
entry on the same existing SQL fixture and database lease.

Next activation work is a combined integration design: selected immutable execution
input (separate from metadata-only source reads), root allocation capacity, explicit
manager review and waiting-step completion, cancellation/refresh and Workflow ownership.
Do not remove the executor's policy guard merely because initial binding exists.
