# Process Outcome Authority with Microsoft Agent Framework 1.15

## Context

Microsoft Agent Framework 1.15 includes the cumulative 1.14 workflow fixes for
terminal output selection, message ordering, session restoration, and approval
response binding. The process integration still owns application-specific
finalizer validation, managed artifacts, tool receipts, and completion gates.

Two compatibility behaviors created competing completion authorities:

- MAF could recover an outcome from any existing step artifact, including an
  artifact left by an earlier execution attempt.
- the Processes module could replace the current nonterminal finalizer outcome
  with `Completed` based on artifact text or fuzzy self-evidence heuristics.

Branch-scoped product receipt rules were also flattened into unconditional
preflight requirements before a branch had been selected.

A live `software-delivery` proof exposed a separate integration mismatch:
runtime-owned verification of an already-existing product correctly performed
no mutation, while completion still evaluated the persisted mutation authority
as an obligation to produce a mutation receipt.

The same proof exposed an authorization-boundary projection defect. MAF 1.15
provides typed function arguments, but path collections were reduced to a
truncated audit string before policy evaluation. The policy consequently
treated an authorized image collection as one malformed scalar path.

A later nested-process proof exposed a second lossy projection. The runtime
verified a child handoff and selected its typed accepted branch, but synthesized
the parent artifact with only the child ref. Downstream QA intentionally cannot
read undeclared child-run artifacts, so the accepted child payload was outside
its trust boundary and missing narrative was incorrectly routed as product
repair.

A Calculator repair proof exposed an asynchronous completion-path mismatch. MAF
persisted a schema-valid required-finalizer result after the provider stopped,
but claim recovery called the result converter directly. That bypassed the
normal staged managed-artifact materialization path and produced a false missing
write-receipt blocker. The bounded finalizer-repair prompt also described a
missing process-owned primary artifact as a reason to block even though the
Processes module owns safe materialization after non-artifact proof is complete.

Fresh Calculator and Tetris proofs exposed a provider transport defect after
otherwise successful work: Terra occasionally completed a request with no text,
tool calls, approvals, continuation token, or output tokens. The terminal MAF
guard correctly rejected that response, but no provider-level bounded retry
preceded the failure. Replaying the process step would be too late and could
repeat product side effects.

## Responsibility inventory

| Responsibility | Owner |
|---|---|
| Provider/session/tool-call execution and required-finalizer repair | `CanDoItAll.AgentFramework.Maf` |
| Canonical process step outcome and durable result receipt | Processes Runtime |
| MAF-to-process translation, managed artifact materialization, and completion gates | `CanDoItAll.Modules.Processes` |
| Product-specific completion policy | process template plus module integration |
| Calculator/Tetris inputs and output roots | project launch data, never Runtime code |

The generic Processes Runtime remains independent of MAF and of .NET product
types. Dependencies continue to point from the Processes module toward the MAF
and generic process contracts, never in the reverse direction.

## Decision

1. A valid current finalizer result is the authoritative step outcome.
2. MAF artifact recovery remains a bounded provider/finalizer repair, but it
   requires:
   - an explicit canonical `Status` field;
   - a successful overwrite trace from the current execution; and
   - a structured trace target matching the exact current step primary artifact.
3. The Processes module materializes evidence only for `Completed` outcomes. It
   does not promote `Blocked`, `Failed`, or `WaitingApproval` to `Completed`.
   Only `Completed` enters completion gates, completion-issue branch routing,
   managed-artifact acceptance, or produced-artifact hashing.
4. Before branch selection, preflight includes only unconditional product and
   capability-scope receipt tools. Branch-specific receipts remain enforced by
   completion gates after the selected branch is known.
5. The MAF 1.15 native approval/session and workflow terminal-output behavior is
   retained. The dead transcript-replay compatibility hook is removed.
6. Automatic recovery guidance may command unconditional receipt tools directly,
   but it must not flatten mutually exclusive branch-scoped rules into one tool
   list.
7. A runtime-owned executor may select the typed
   `ReadOnlyProductVerification` completion scope only. The coordinator derives
   the effective assignment by removing `MutateProductTarget` and narrowing
   `ExternalProductTargetMutable` to `ExternalProductTargetReadOnly`. It rejects
   any other persisted target, missing mutation authority, missing managed
   artifact authority for produced slots, or any product-mutation receipt.
   Every other persisted operation and its completion gates remain in force.
   This scope is internal runtime state and cannot originate from MAF output,
   model structured output, or a managed artifact.
8. Tool authorization consumes a Core-owned typed path projection built from
   MAF's function arguments. Scalar paths and every element of a path collection
   pass through the same generic current-project, current-run, and external
   target boundaries. Any unsupported collection shape or unauthorized member
   denies the entire invocation. Redacted and length-bounded argument strings
   remain an audit/signature projection only.
9. A runtime-owned subprocess bridge accepts only the exact child artifact
   named by the typed child-output contract when it is readable, contains the
   runtime accepted-completion heading, declares the required branch, and its
   SHA-256 content hash equals the single produced artifact on the child step's
   current `CompletedResultKey` receipt. Historical receipts and duplicate slot
   artifacts are not eligible. A branch-discriminated mapping requires exactly
   one canonical managed-artifact branch key.
10. The synthesized parent artifact preserves the selected child bytes in one
    runtime-owned envelope. Citation sanitization treats verified envelopes as
    opaque, while fenced framing allows authenticated envelopes to be composed
    recursively without confusing nested markers for outer framing. The typed
    accepted/no-go disposition and parent branch remain canonical; downstream
    agents may use the authenticated payload as context but cannot re-decide the
    route from Markdown. Missing, duplicate, tampered, or untrusted outer
    envelopes fail closed.
11. Workspace text reads expose one shared 64,000-character complete-read
    boundary. Runtime subprocess envelopes reserve 16,000 characters for the
    parent managed-artifact wrapper and cap their combined payload at 48,000
    characters. Grounding and produced-artifact hashing reject truncated reads;
    they never sanitize, overwrite, or hash a preview as if it were complete.
12. A recovered completed MAF execution follows the same
    `ProcessStepCompletionCoordinator` path as a synchronously observed
    completion: normalize and stage the typed outcome, aggregate bounded
    current-step receipts, run every ordinary completion gate, append acceptance
    only after those gates pass, and hash exact final readback. Recovery must not
    call the result converter directly.
13. During bounded required-finalizer repair, absence of the configured primary
    managed artifact alone is not a blocker when all non-artifact work and
    required current-run proof succeeded. The agent submits the typed
    `Completed` outcome with the applicable declared branch and existing
    evidence refs; the Processes module derives and materializes the canonical
    ref before completion gates. This does not waive product mutation,
    validation, capability, schema, or receipt requirements, and it does not
    authorize promotion of a `Blocked`, `Failed`, or `WaitingApproval` outcome.
14. Runtime materialization requires the persisted assignment to authorize
    `WriteManagedProcessArtifacts`. Before the first write, the generated staged
    content plus its prospective acceptance appendix must fit inside the shared
    complete-read boundary. Before appending a captured-outcome or accepted-gates
    ledger section to an agent-written artifact, the runtime completely reads
    the current body and proves that the exact body-plus-append length remains
    inside the same boundary. Unauthorized, unreadable, truncated, or oversized
    outcomes fail before mutation, so a lifecycle marker cannot strand an
    artifact beyond the canonical hash/readback limit.
15. Parent completion blocker detection treats an exact, receipt-backed
    subprocess child-output or forwarded-context envelope as authenticated child
    evidence rather than parent-authored state. It continues to inspect the
    parent reason, branch data, evidence refs, next actions, and summary text
    outside verified envelopes. Missing, mismatched, altered, or otherwise
    unverified envelopes remain subject to the ordinary blocker scan and fail
    closed through the existing grounding gates.
16. A normal, safe, idempotent completion-gate diagnostic may receive bounded
    automatic current-step rework only when the immutable assignment has the
    managed-artifact-only or external-product-read-only target scope. Its
    operations are limited to process-context reads, project-structure reads,
    upstream-artifact reads, managed-artifact writes, and artifact-only
    recovery, and it must include managed-artifact write or artifact-recovery
    authority. The external-product-read-only scope permits passive product
    inspection but no product mutation or executable validation. Mutable
    external-product scopes, decision/escalation tools, runtime-proof capture,
    validation, runtime launch, external actions, child-process launch,
    external-destination writes, product mutation, restricted diagnostics,
    approval-bearing plans, and exhausted retry budgets remain ineligible.
    Passive stored-image inspection uses project-structure read authority; it
    does not receive interactive browser authority.
17. Content inside a required upstream managed artifact may ground a downstream
    path only through the typed step execution contract. The required input must
    be uniquely available for its slot, identify a concrete artifact, carry a
    content hash, and map to exactly one managed Markdown descriptor. The runtime
    rejects duplicate descriptor records even when they normalize to the same
    ref, then performs a complete bounded read and requires the physical bytes
    to match that hash before using either the canonical ref or body for
    grounding. Artifact refs discovered in prompts, launch prose, or other
    untyped strings never authorize recursively trusting the file body.
18. Before managed-artifact materialization, the Processes module may omit a
    complete ungrounded evidence-ref entry from only two non-authoritative
    locations on a typed `Completed` outcome. Supplemental top-level
    `EvidenceRefs` are eligible only on a non-acceptance defect route whose
    branch key matches exactly one declared agent-selectable branch, whose typed
    failed-criterion evidence and all narrative and criterion-level refs are
    grounded, and whose canonical managed-artifact refs remain after omission.
    A criterion-level entry is eligible only when its typed status is
    `NotVerified`, the outcome is unbranched or its branch key matches exactly
    one declared non-acceptance branch, and every top-level, narrative,
    criterion-id, criterion-summary, `Passed`, and `Failed` path ref is already
    grounded. The runtime preserves the `NotVerified` criterion, status,
    summary, and grounded or non-path refs; it never edits or infers a rejected
    path. Accepted branches are ineligible. Both policies record only an
    omission count and runtime identities without the rejected literal, then run
    the unchanged whole-artifact grounding, receipt, mutation, branch,
    acceptance, and completion gates. Any malformed authoritative or acceptance
    evidence still fails closed.
19. Repeated tool-invocation governance is owned by the Core tool policy rather
    than a second MAF streaming guard that can terminate the provider before it
    receives ordinary tool-policy feedback. An authorized browser interaction
    starts a new validation epoch and is not deduplicated by signature:
    identical clicks, key presses, and form actions can be intentional runtime
    interactions. The epoch records an allowed invocation attempt, not proof
    that browser state changed or that the tool succeeded. Navigation,
    observation, evidence-capture, source-mutation, and non-browser validation
    invocations retain bounded repetition within the applicable validation
    epoch.
20. A kept-alive `ExecutionRun` workspace process is owned before process-host
    invocation. The reviewed launch plan must expose exactly one normalized
    `startup.json` target. Every launch receives a timestamp-plus-random-nonce
    directory identity so concurrent starts cannot share a receipt path. The
    command service persists that identity as a `Pending` lease before launch,
    matches the successful command receipt back to the same identity, and then
    activates it. An ordinary failed launch
    removes its pending intent, while host termination retains it for bounded
    terminal recovery. Caller arguments, stdout, process-template text, and
    receipt-summary parsing are not authorities. `WaitingOnTool` retains the
    lease across MAF runtime disposal. The public cleanup boundary re-reads the
    durable execution run and denies empty, missing, `Running`, and
    `WaitingOnTool` identities; raw lease cleanup is not part of the public
    workspace-command contract. Cleanup begins only after a `Completed` or
    `Failed` execution mutation is durably persisted, including ordinary
    completion, provider failure, cancellation, approval continuation, and
    host-restart recovery. Concurrent cleanup is serialized per lease, attempts
    every owned lease, records typed stop receipts under the execution audit
    scope, retains unavailable pending receipts and failed stops for startup
    retry, and never replaces the primary execution outcome. `ProcessRun`
    lifetimes remain owned by the process graph and are never registered as
    execution leases. Runtime-owned cleanup is infrastructure lifecycle
    evidence; it does not create an agent tool invocation or satisfy an agent's
    runtime-proof completion gate.
21. Every supported provider transport is wrapped at the MAF 1.15
    `IChatClient` inference boundary. A fully completed provider attempt may be
    retried exactly once only when it contains no non-whitespace text, tool or
    approval content, unknown actionable content, positive output tokens,
    continuation token, or non-stop terminal disposition. The first attempt is
    buffered before tool execution, and its usage remains in the response
    stream or aggregate. Any actionable content, cancellation, provider
    exception, background-enabled agent or continuation, unsafe finish reason,
    or provider-hosted or otherwise unknown non-function tool disables the
    retry. Disabling the entire background-enabled agent path prevents replay
    when a malformed response omits a continuation token after provider work
    has already started. The hosted-tool rule prevents replay after a hosted MCP
    or native tool may already have acted beyond the local
    function-invocation boundary. The existing terminal
    empty-response guard remains authoritative after the second attempt.
    Recovery, suppression, and exhaustion are observable through correlated
    logs, an activity, and a counter; none can fabricate a process result or
    consume process-recovery authority.
22. Azure OpenAI chat and Responses use the OpenAI 2.12 client directly against
    Azure's stable `/openai/v1/` endpoint. `Azure.AI.OpenAI 2.9.0-beta.1` is
    removed from this runtime because it is binary-compatible only with the
    OpenAI 2.9 line, while MAF 1.15 and Microsoft.Extensions.AI 10.8 select a
    newer OpenAI client. Bare Azure resource URLs and explicit stable v1 URLs
    normalize to one endpoint shape. Legacy deployment URLs, query-bearing
    URLs, and fragments fail explicitly instead of selecting an ambiguous
    protocol. Provider kind, model/deployment selection, credential ownership,
    and the generic MAF/process boundary remain unchanged.
23. The application creates one typed `ProcessDispatchClaimIdentity` from the
    active runtime claim immediately before strategy invocation. The dispatcher
    carries it through the generic strategy and adapter contracts, and the
    Processes integration records it in an owned execution-metadata field.
    Recovery requires an exact recorded identity match before selecting,
    releasing, or submitting an execution; run id, step id, and half-open claim
    lease time remain sanity bounds only. Missing, empty, malformed, or
    mismatched identity fails closed.
24. Managed-artifact lifecycle mutation is serialized from marker inspection
    through acceptance append and final content hashing within one application
    process. The current deployment invariant is one active process-runtime
    writer for a workspace. The in-process gate is not multi-host coordination:
    a future multi-host deployment must replace it with storage-backed compare
    and swap or a distributed lock covering the same complete critical section
    before concurrent writers are supported.
25. The process runtime's step execution timeout is an end-to-end attempt
    deadline. It includes strategy construction, adapter work, provider-capacity
    admission, MAF composition and inference, tool turns, required-finalizer
    handling, and result conversion. The conservative defaults for the current
    managed profiles are 60 minutes with a 75-minute dispatch lease.
    Deployments must align this generic deadline and lease with their executor
    and provider bounds plus the expected admission-queue service level. A
    future active-execution budget requires a typed, provider-neutral adapter
    lifecycle persisted as queued, executing, and progress observations;
    ephemeral MAF activity or log projections cannot pause a canonical process
    clock. Timeout never grants automatic replay authority because tools or
    product mutations may already have occurred.
26. Framework-wide detailed tool errors remain disabled. A tool failure is
    agent-visible only when its owning boundary explicitly implements the safe
    `IAgentToolFailure` contract; unknown exceptions retain the framework's
    generic failure response, and their raw messages are replaced before tool
    traces or receipts become durable evidence. The MAF adapter returns a
    reviewed failure as a typed `Succeeded=false` result, so it cannot satisfy a
    required-tool receipt and the model may correct only the declared input. Project-structure
    asset creation and revision expose agent-specific DTOs without raw metadata
    JSON, leaving canonical storage metadata derived by the owning service.
    Metadata correction guidance is emitted only after validating metadata
    explicitly supplied to a metadata-capable mutation. Metadata failures while
    reading persisted state remain hidden operational failures.
27. Browser evidence requested by a process step must be written beneath that
    current process run's managed-artifact directory. A bare filename is not
    current-run evidence and cannot satisfy inspection, comparison, or positive
    interactive-acceptance gates. The generic step brief supplies the exact
    run-scoped browser root, while the completion gate independently validates
    the persisted `filename` on successful post-interaction state receipts.
    Visual repair guidance also keeps semantic application data authoritative:
    when a reference requires fixed presentation capacity that exceeds current
    data, the repair must use an explicit presentation-only mechanism and prove
    both sparse and populated states. It must not invent domain records or rely
    on layout rules to create DOM content that does not exist.
28. A completed provider turn with no valid required finalizer enters the
    bounded finalizer-only repair path before managed-artifact recovery. The
    repair exposes only the required finalizer tool, followed by a typed JSON
    fallback for the same output contract; it cannot repeat workspace, product,
    runtime, or browser operations. Process-step artifact recovery is the last
    fallback and remains subject to ordinary completion gates. Acceptance
    evidence uses the exact typed properties `criterionId`, `status`, `summary`,
    and `evidenceRefs`. Criterion-level path refs pass through the same
    current-run grounding validator as top-level refs; aliases and Markdown
    criterion tables never become canonical acceptance authority.
29. A forwarded child-context artifact is read completely up to the workspace
    text-read ceiling and verified against the child's accepted input ledger.
    The authoritative prompt-size constraint is the existing combined
    subprocess-envelope budget, which reserves managed-artifact readback
    headroom across the verified child output and every forwarded context
    artifact. A second per-artifact 16,000-character policy is not applied:
    it rejected valid, hash-grounded enterprise context before the aggregate
    boundary could evaluate it. Content above the complete-read ceiling,
    hash mismatch, and an oversized combined envelope still fail closed.
30. Claim expiry distinguishes work that has not started from work whose side
    effects are indeterminate. An expired claim may return to `Ready` only
    while its step is still `Claimed`. Once the step is `Running`, expiry
    blocks the step and run with a typed replay-unsafe diagnostic. Startup
    recovery applies the same invariant: an exactly claim-bound failed or
    cancelled Agent Framework execution is consumed through the normal typed
    result path as `NeedsManager`, never released and re-enqueued. Exact run,
    step, claim, terminal state, and half-open lease membership are mandatory;
    missing or mismatched identity fails closed.
31. Branch authority is carried from the compiled instance plan into the typed
    step execution contract. A completed agent step may route only when its
    finalizer returns exactly one `BranchOutcomeKey` that ordinally matches a
    configured outcome for that step. Prompt prose, titles, summaries, and
    managed-artifact sections are evidence, never routing authority. The
    runtime branch router independently requires exactly one matching compiled
    route before changing any gate; an unknown, duplicate, or case-mismatched
    signal blocks the responsible step and run with an unsafe-to-retry typed
    diagnostic while leaving every downstream gate unchanged.
32. Artifact-assisted finalizer recovery uses the shared complete-text read
    boundary. The artifact must be readable in full, non-truncated, internally
    length-consistent, and no larger than 64,000 characters. Oversized or
    partial artifacts cannot synthesize finalizer authority, and recovered
    artifact content never supplies a branch outcome key.
33. The generic .NET quality-repair subprocess has exactly three explicit,
    evidence-driven mutation opportunities. A failed second independent
    revalidation selects the compiled
    `final-specialist-repair-required` branch; it is not yet terminal no-go
    evidence. The final lane has unique diagnosis, mutation, validation, and
    handoff step and artifact identities. Its diagnosis must consume both prior
    failed proof chains through their canonical diagnosis, change-set, and
    validation artifacts, then prescribe a meaningfully different falsifiable
    action. The final accepted handoff and terminal no-go consume the complete
    canonical artifact chain directly rather than a model-authored history
    projection. Diagnosis and validation remain read-only, the implementer
    cannot accept its own work, and the existing mutation, readback, build,
    test, runtime, browser, and required ProductAcceptance criterion gates
    remain in force. Generic UI repair guidance expresses falsifiable
    presentation and semantic-data invariants without prescribing a DOM or CSS
    implementation technique. Only final independent revalidation can select
    `quality-repair-no-go`. There is no backward route, recursive subprocess,
    model-controlled retry count, or mutable loop ledger outside the compiled
    definition snapshot.
34. A required finalizer always exposes the schema of its governed output
    contract, including when the runtime deliberately captures a raw JSON
    element for tolerant normalization. Its invocation has exactly one
    top-level `result` argument. Unknown sibling arguments are rejected before
    binding rather than silently discarded, and process acceptance-criterion
    evidence must therefore remain inside the typed result object. Tolerant
    normalization may repair known shapes inside `result`; it cannot widen the
    finalizer protocol.
35. Typed failed acceptance-criterion evidence remains the primary authority
    for a repair branch. A driver-owned observed-defect contribution is a
    bounded fallback only when it can bind a positive, non-negated framework
    defect claim to exact successful receipts and managed evidence references
    from the current execution. That contribution may recognize standard
    framework error surfaces, but it must not contain application names,
    product rules, or workflow-specific branch exceptions.
36. Generic Blazor validation derives topology and required capabilities from
    the resolved execution contract. Browser proof is captured after the
    current startup receipt in a fresh page or context; pre-start DOM, console,
    screenshot, cache, or service-worker state is not current-runtime proof.
    API, persistence, PWA, test-project, and directory requirements apply only
    when the resolved contract declares them.
37. Launch-time acceptance extraction is source-authoritative. Selected source
    items and typed product requirements may contribute implicit required
    criteria; contextual work items require an explicit acceptance section;
    contextual operational material remains non-blocking delivery-planning
    context unless it declares explicit acceptance or definition-of-done
    criteria. Visual target assets remain product-acceptance inputs. Explicit
    list items remain whole criteria rather than being split on punctuation.
38. Review branches distinguish defects from missing proof. `repair-required`
    and `repair-escalation` require a typed failed criterion or equivalent
    deterministic current-build product defect. `NotVerified` requires the
    authorized proof action, or a branchless `Blocked` outcome when a current
    failed tool, input, access, or contract receipt prevents that proof.
39. A governed project-structure mutation below an existing external-root
    ProjectBlock may inherit only root authority already bound into the current
    audited execution. The write guard converts a stored physical root through
    the execution's protected root bindings and compares the resulting alias to
    the current allowed or read-only alias set. Missing, foreign-host, malformed,
    or unrelated bindings remain denied. This equivalence check changes no
    stored root and cannot mint new execution authority.

## Rejected alternatives

- Accepting an artifact by run id, step id, and step key alone was rejected
  because those values are shared by retries.
- Inferring `Completed` from arbitrary nonempty Markdown was rejected because it
  silently converts missing protocol data into success.
- Removing all finalizer repair was rejected because provider completion and
  timeout failures still exist independently of workflow terminal merging.
- Moving .NET setup or Calculator/Tetris policy into the generic Runtime was
  rejected because those concerns belong to drivers, templates, and launch data.
- Allowing an executor to submit an arbitrary operation subset was rejected
  because it could suppress validation, runtime-proof, or external-action
  receipt obligations.
- Parsing path collections back out of redacted audit strings was rejected
  because truncation makes that representation incomplete and authorization
  must not depend on a lossy projection.
- Requiring downstream agents to read child-run paths or restate accepted child
  prose was rejected because it breaks the declared artifact trust boundary and
  makes routing depend on duplicated, model-authored narrative.
- Replaying an entire product-mutating assignment merely to create a missing
  process-owned evidence file was rejected because it repeats side effects and
  widens recovery authority.
- Treating assistant prose or a denied/missing artifact write as artifact
  recovery evidence was rejected. The only no-write success path starts from a
  schema-valid typed `Completed` result and still passes the normal process
  completion gates.
- Copying an accepted child's branch into an unbranched parent step, weakening
  blocker phrases, or enabling parent completion with arbitrary open issues was
  rejected. Those approaches confuse child evidence with parent lifecycle state
  and could suppress genuine parent-owned blockers.
- Trusting malformed path references, repairing GUIDs heuristically, or replaying
  validation, runtime-launch, external-action, or product-mutating assignments
  was rejected. A rejected citation remains rejected; only a bounded replay of
  the declared managed-artifact or read-only proof contract may replace it with
  freshly grounded output.
- Recursively trusting the body of any managed artifact whose path appeared in
  prompt text was rejected because prompt discovery is not provenance and lets
  untyped prose widen the downstream evidence boundary.
- Replaying QA or heuristically correcting a malformed GUID in a citation was
  rejected because either repeats product/runtime effects or fabricates a new
  evidence claim. Broadly dropping ungrounded refs was rejected because it could
  erase criterion evidence. Only a malformed supplemental top-level entry on an
  otherwise grounded typed defect route, or one complete ref on a preserved
  typed `NotVerified` criterion in an otherwise grounded non-acceptance outcome,
  is eligible. `Passed`, `Failed`, accepted-branch, narrative, summary, and
  identifier evidence remains strict.
- Counting identical browser interactions cumulatively across an entire agent
  run was rejected because the same control or key is routinely used in
  multiple distinct runtime scenarios. Throwing from the MAF response stream
  was also rejected because it bypasses the canonical tool-policy decision and
  converts a policy concern into a provider failure.
- Cleaning kept-alive processes from `RuntimeBuildResult.DisposeAsync` was
  rejected because MAF runtime objects are also disposed while an execution is
  waiting for approval. An in-memory runtime-local registry would either stop
  too early or lose ownership across continuation scopes and host restart.
  Removing a lease by an unscoped caller path was rejected because only the
  owning execution run plus the successful typed stop receipt may release that
  durable identity.
- Registering ownership only after a successful process-host return was
  rejected because host termination between child launch and lease commit would
  leave no recoverable owner. Trusting lifecycle callers to invoke cleanup only
  after terminal persistence was also rejected; the public cleaner must verify
  the durable execution state itself before reaching the raw cleanup executor.
- Classifying provider-empty completion as a process-level transient failure
  and replaying the assignment was rejected. By then the assignment may already
  have mutated the product, and its no-side-effect attestation is intentionally
  advisory rather than replay authority. Agent middleware and MAF workflow
  loops were also rejected for this fault because they wrap a wider semantic
  lifecycle than one raw inference and can duplicate tools. The MAF 1.15
  `IChatClient` factory seam is the narrow boundary that can prove the first
  provider attempt emitted no actionable content before retrying it.
- Retaining `Azure.AI.OpenAI 2.9.0-beta.1` beside OpenAI 2.12 was rejected
  because Azure Responses fail at client construction with a missing binary
  constructor. Downgrading OpenAI would conflict with the MAF 1.15
  Microsoft.Extensions.AI dependency line. The supported stable Azure v1
  endpoint lets the same current OpenAI SDK serve both provider kinds without
  a second incompatible client stack.
- Treating provider admission as active execution time while keeping the former
  20-minute deadline was rejected because the provider lane can legitimately
  queue one governed run behind another and MAF's own bounded provider call may
  outlive that outer deadline. Automatically retrying a timed-out step or
  inferring success from a recently written artifact was also rejected because
  either can repeat side effects or bypass the typed finalizer and completion
  gates.
- Enabling `IncludeDetailedErrors` for all MAF tools was rejected because it
  would expose raw exception messages and infrastructure state. Inferring safe
  disclosure from HTTP-style status codes was also rejected: status does not
  prove that an exception message is suitable for an agent. A narrow,
  owner-authored safe failure contract preserves correction without weakening
  the default fail-closed boundary.
- Treating browser filename guidance as sufficient enforcement was rejected.
  A successful receipt proves the browser tool ran, but a bare provider-native
  file is not imported as current-run managed evidence. Positive acceptance now
  requires an explicit current-run managed filename, matching the provenance
  already required for negative visual-defect evidence.
- Truncating, schema-projecting, compressing, or forwarding only a child-run
  reference was rejected as a response to large forwarded context. Truncation
  breaks the accepted ledger hash, schema projection makes the generic runtime
  own domain payload semantics, compression creates a derived source of truth,
  and reference-only forwarding widens cross-run read authority. The aggregate
  authenticated-copy budget remains the single generic handoff-size policy.
- Releasing an expired `Running` claim or a failed/cancelled recovered
  execution back to `Ready` was rejected because the agent may already have
  mutated the product, launched a runtime, or invoked another external side
  effect. A durable execution identity proves ownership, not non-execution;
  therefore interrupted post-start work requires an explicit operator-owned
  disposition rather than automatic replay.
- Recovering a branch outcome from prompt text, artifact headings, summary
  prose, or case-insensitive signal matching was rejected because it creates a
  second, string-derived routing authority outside the compiled plan. Trusting
  an unmatched signal and skipping every nonmatching gate was also rejected;
  invalid route identity must preserve all gates and block predictably.
- Reading an arbitrary-size artifact to synthesize a missing finalizer was
  rejected because it bypasses the canonical workspace text boundary and can
  promote a partial or unbounded payload. A preview cannot be completion
  authority.
- Reopening terminal repair steps, interpreting an unused loop budget, or
  recursively launching the quality-repair subprocess was rejected. The
  current branch router opens only precompiled forward gates, subprocess cycles
  are forbidden, and repeated step keys would overwrite managed-artifact and
  completed-result identities. Merely launching another fresh root was also
  rejected as the durable policy because it retains the same inadequate repair
  topology. A fresh root is used only to validate the new snapshotted DAG.
- Accepting unknown sibling finalizer arguments and trying to recover their
  meaning later was rejected because model binding can discard them before the
  governed output exists. Weakening repair-branch evidence requirements was
  rejected for the same reason: it would turn a protocol defect into routing
  authority.
- Treating browser state captured before the current startup as proof, or
  prescribing one test layout, persistence abstraction, API, or test framework
  in the common Blazor process, was rejected. Those choices belong to the
  resolved product contract and source graph, not the process runtime.
- Weakening the project-root guard, trusting a stored physical path by itself,
  or rewriting operator-selected roots to execution aliases was rejected. The
  protected binding is the only proof that the stored physical root and the
  current execution alias identify the same authorized namespace.

## Validation

- focused MAF finalizer and process adapter unit tests;
- finalizer argument-shape, typed-schema, and nested acceptance-evidence tests;
- generic Blazor template projection and post-start browser-proof tests;
- process architecture baseline tests;
- solution build and test suite;
- live `software-delivery` runs for Calculator and Tetris using Terra or Luna
  agents, with no manager escalation;
- final 5032 health and dispatch-queue verification.
