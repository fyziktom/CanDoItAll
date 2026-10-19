# Workbench planning participant and actual saved publication

The provider, saved-action owner and v2 SQL publisher now have focused proof. Connect
them through the existing module/tool boundaries before claiming product activation.
Use the actual configured MAF planning port, file journal, selected publication lease,
tool policy pipeline and PostgreSQL task/receipt owner in the integration fixture.

## Responsibility and data flow

Workbench owns a bounded, strict task-plan participant and the saved effect it emits.
Core owns decision/work transitions; MAF owns SDK transport and the existing saved
effect dispatcher. Reuse Workbench's existing Core/Models/Tooling references. Do not
add a module reference to Core/MAF or put Workbench parsing into the runtime.

The immutable original target includes the exact target project lifetime and captured
Agent source grants/governance, including any source project lifetime. Capture it before
the first planning-run publication. No current read may replace that original target.
Root Workspace policy and Agent override provenance must be captured by the actual
admission path, without silently inventing a missing Workspace policy.

The participant captures the existing canonical complete task basis and finite-operation
write context in one bounded basis envelope. RunSnapshot reuses that envelope within a
generation; StepSnapshot recaptures once per logical step; Refresh recaptures under its
new generation. An epoch that closes meanwhile produces an explicit refusal. Never put
elapsed or remaining work into semantic identity.

Accept only the existing flat task draft: version, task title/description/notes/priority,
bounded count and bytes, with no assignments, estimates, parent commands or unknown
members. Reject duplicate JSON members and missing/null required values, and reuse the
owner normalization. No invalid response triggers a hidden provider retry. Publication
preparation is pure and allocates one original operation identity after the response.

Source preparation stays below the existing 64 KiB bound. Bind decision/generation,
logical step, basis identity/digest, original operation and semantic source fingerprint;
do not copy the full basis into it. Recover the referenced original decision through the
verified existing session/invocation/receipt observations. If an additional neutral
planning observation is needed, it carries the immutable decision and exact run/journal
revisions from the already-verified snapshot; it does not create another store or read
pipeline. Current-read callbacks must use their supplied observation and avoid recursive
calls back into the same verifier.

Register a narrow Workbench saved-effect provider/receipt owner through the existing
tool-provider composition. It restores only the original planning publication; it does
not advertise an ordinary model-callable bypass. Keep the existing mutation policy,
exact approval, original receipt preflight and one dispatch claim. The broad existing
ProjectStructure tool provider does not need another nested tool implementation.

## Publication and result boundary

Before SQL, obtain the current exact observation and acquire the existing selected
run/Agent/chat snapshot lease. Reuse `ProjectAgentSourceMutationAuthority.RequireSource`
with that held catalog rather than reacquiring it. Bind the original task domain and
operation ceiling, current grants, profile generation, target and source lifetimes.
Supply the source lifetime as an additional project guard. Under SQL, authority and
active-work checks use only held data and the owner-issued capability; they must not
read files, call a provider or wait for a person. Review deletion/cleanup lock order:
Agent access deletion prepares SQL intent and publishes catalog cleanup after commit.

Use the same trusted caller identity and source fingerprint for submission and receipt
lookup. Preserve known commit, unknown commit, exact replay, cleanup-incomplete and
current-disclosure distinctions. Receipt observation never consumes new mutation work.
Historical/uncertain receipt recovery must resolve the retained original decision and
operation without requiring a fabricated live work capability.

The actual module result must carry a bounded provider-neutral owner outcome to the
planning coordinator. Core must not decode MAF's private SDK result checkpoint and MAF
must not interpret Workbench JSON. Prefer an opaque typed owner envelope passed by the
registered tool result through the existing saved-dispatch result; keep the original
checkpoint and owner receipt as durable truth. Only an acknowledged definite no-effect
BasisChanged result may pause/replan. Other refusals and unknown commits cannot be
relabelled as changed material. Loss of the live monotonic anchor still forbids resume
or replan, even if receipt reconciliation can later establish an effect's outcome.

## Proof and subsequent activation

Run provider-to-approved-publication tests through actual configured MAF transport
(intercepted external HTTP), real file owner and PostgreSQL. Prove exact accepted tasks
and receipt, stale material no-effect refusal, current permission revocation, target
deletion/recreation, held work expiry during a project wait and before commit, replay
without another provider/effect, and known/unknown receipt recovery. Include RunSnapshot,
StepSnapshot and Refresh/replan with distinct intent and renewed approval. Test strict
parsing and cross-decision/basis/operation restoration refusal independently.

Build/discover the affected standard Release projects, review dependencies, enforce the
full portability baseline, freeze, start/verify the observer before tests, execute and
reconcile. Actual interactive admission and UI actions follow through the existing
Workspace execution entry and host seams. SPA changes stay on `codex/concurrent-mutations`;
do not merge SPA main. Process/workflow policy capture, remaining writer perimeters,
capacity and final broad validation remain separate required closure work.
