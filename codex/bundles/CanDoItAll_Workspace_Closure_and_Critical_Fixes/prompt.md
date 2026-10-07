# Execute: Workspace closure, shared lifetime repairs, and verified regression readiness

You are Codex GPT-6 Astra Max acting as the senior C#/.NET and Blazor engineer for this closure.
Implement and verify the bounded corrections below on the current checkout. Do not start a new
module, create another UI extraction, or merely return a plan. Keep the existing Workspace UI
architecture, owners, sandboxes and security behavior.

## Objective and actual entry state

The reviewer inspected `fyziktom/CanDoItAll`, branch `components-decoupling`, implementation
`d9273a88973d28d73c4d29f8686f2b3d68ef8f3a`, whose parent `1080c24163afd3cf65fcc68756913c5dd3262a62`
is the archived prior handoff. The earlier implementation was `fbfba65de9d3118729b73ce9dcf97f28a219c84c`.
These identify provenance; never reset, switch or cherry-pick to a review SHA merely to match
this document. Reconcile drift against actual source and preserve unrelated work.

Workspace's renderer extraction is substantially complete, including Recovery, Data Sources
and Configuration. That is not a declaration of correct application behavior. The maintained
campaign explicitly leaves three shared lifetime defects and two incomplete live journeys open.
This review additionally identifies WCL-R1 at the real Data Sources edit boundary. Reproduce
and close those items, then validate the composed application and report a truthful verdict.

## Read before edits

Read this complete package, starting with REVIEW, TEST_RESULTS_REVIEW, FIX_DATA_SOURCES,
FIX_SHARED_LIFETIMES, DEPENDENCY_DELIVERY, LIVE_VALIDATION, VALIDATION_MATRIX and CLOSURE.
Read the shared foundation's boundaries/state/effects guidance, but do not rerun its historical
module tasks. Read current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`,
`.github/workflows/ci.yml` and `docs/architecture/ui-component-seams.md`. Follow the current
CanDoItAll.SharedInfo standards and the instructions of each repository you modify.

Read the current maintained Workspace boundary/census/campaign documents and all three
shared-lifetime finding documents. Recover the original ignored artifacts when present.
Verify their hashes and source context. An unavailable artifact stays unavailable; do not
manufacture its prior trace, timing or execution result from the Markdown report.

Use CodeAnalytics/Components/dotnetwatch MCP tools when actually available. Otherwise use
current source, evaluated MSBuild graphs, owning tests, actual sibling component contracts,
CLI watch and Playwright, and record the fallback. Never let a search index of the default
branch stand in for source on the current branch.

## C0 — Entry, provenance and baseline

Record application HEAD/tree/dirty paths, Components/FileTools/SharedInfo/Mcp revisions as
applicable, SDK/runtime, configuration and dependency selection. Keep archive-only changes
separate from executable changes. Capture protected Workspace leaf graphs and watched inputs.
Do not alter already finished UI projects to reduce a cosmetic project count.

Reserve private PostgreSQL 18 databases, control-plane/vault/browser/file roots and process
ports. The ordinary application on port 5032 and retained customer/developer data are not
fixtures. Inspect prerequisites for the previously blocked browser cases before the long
run. Provision only task-owned authorized instances; preserve missing prerequisites as
BLOCKED and continue independent work rather than silently excluding tests.

Copy `templates/closure-results.json` to an ignored artifact root. Keep attempt history
append-only and source/checkpoint hashes accurate. The template starts NOT_RUN, not green.
Establish concise UTC-stamped test-host logging with host/circuit/read/operation correlation
where feasible; do not change business protocols merely to obtain diagnostic correlation.
A source manifest SHA-256 is not a Git commit ID.

## C1 — WCL-R1: enforce exact profile editing at actual persistence acquisition

Implement FIX_DATA_SOURCES. The new WorkspaceDataSourcesOwner checks exact existence before
calling the legacy profile Save. The coordinated control-plane Save still upserts when that
record is gone. A separate thread/circuit/process can delete an inactive saved profile after
preflight but before that separately coordinated write. The unchanged model ID can recreate
it, potentially without its preserved password. This is not prevented by a busy UI.

First write a failing-first deterministic two-owner test across that exact window. Existing
`DataSourcesOwnerTests` only proves deletion before preflight. Add the smallest editor-specific
existence/protection check inside the current catalog coordination, keeping legacy non-UI
upsert behavior where required. Do not introduce new Workspace references into Foundation,
a new schema/incarnation system, a distributed lock or a globally changed Save contract.

Also test the corresponding current/pending/startup protection at write admission; if a
separate race requires changing a wider canonical/activation protocol, map it rather than
hiding it behind a UI recheck. Retain real known acknowledgement facts and distinguish
refusal from unknown. Preserve blank-password retention, exact create IDs, physical database
and file retention on profile deletion, and canonical A-before/B-after-restart semantics.

## C2 — WC-C1: lifetime of Simple Chat reads and scoped dependencies

Implement the WC-C1 section of FIX_SHARED_LIFETIMES. Hold the real catalog query and separately
retire the contributor, shell, circuit and owning service scope. Inspect actual construction
and disposal order. The runner's profile lease is not ownership of its injected database
context; delaying only semaphore disposal does not fix an already disposed DbContext.

Establish explicit ownership that keeps admitted work's actual dependencies alive until it
settles and prevents new work after retirement. Handle waiting operations too. Preserve
profile/generation fencing and operation-scoped execution context. Review all runner callers,
including writes and hosted dispatch; cancellation cannot erase committed work or cause replay.

Keep ConversationShellHost's existing S0 nonblocking initialization, captured token and stale
publication checks. Do not impose a renderer/JS-dependent drain on component teardown or
block a synchronization context with Wait/Result/GetAwaiter().GetResult(). Do not fix the
trace by catching ObjectDisposedException, removing serialization, making the runner global,
or obtaining a fresh current-profile context after the original profile retired.

Require a deterministic reproducer, admitted/waiting/denied/profile/close/reopen controls,
no deadlock, bounded cleanup, current active read behavior and mutation outcome regressions.

## C3 — WC-C2: repair BaseLib Dialog teardown in its owning repository

The earlier bundle kept siblings read-only. This closure authorizes a bounded change in
`CanDoItAll.Components` for Dialog/DialogInterop, their exact JS ownership if required,
focused tests and necessary source/package evidence. It does not authorize unrelated shared
UI redesign, blanket dependency updates, or arbitrary edits to FileTools.

Reproduce cancelled close during disposal. Ensure owned JS module and .NET callback release
is attempted even when close fails; reject late import/open publication and make repeated
retirement safe. Distinguish active explicit close, a retired/disconnected dialog and a real
unexpected interop failure. Do not blanket-suppress active cancellation/faults, delete full-log
assertions, globally disable animations, or replace the component in consumer tests.

Validate actual parent/child and unrelated dialogs, pending imports, cancelled/disconnected
close, module-release faults, keyboard Escape/backdrop and focus. Follow DEPENDENCY_DELIVERY:
the application must consume the exact tested repaired sibling source, not an unrecorded
working-tree patch. Current CI resolves a matching Components branch, not a static Components
SHA setting; inspect it before changing anything. Produce a signed, traceable source-pair
handoff and exact operator publication order. No automatic push/merge or invented existing pin.

## C4 — WC-C3: terminate shell navigation and browser-state effects correctly

Hold each asynchronous stage in MainLayout first-render and location-change processing.
Retire the layout/circuit at each boundary. Prevent retired work from registering listeners,
redirecting a successor route, tracking an obsolete tab or writing a successor profile's
browser state. Use exact layout/route/profile ownership, not a single check before the first
await. Observe queued tasks and retain diagnostic correlation without sensitive values.

Preserve active tab tracking, explicit tab operations, reload/back/forward, startup database
confirmation and restart activation. A disconnected browser did not acknowledge a snapshot;
never turn a store-wide catch into a successful Save. Expected teardown must be contained at
its proper boundary while unrelated active persistence failures remain observable. Preserve
independent live layouts/circuits and do not introduce a new durable workspace-state system.

Run controlled ordering tests, existing BrowserStateStore/Workbench/MainLayout and S0 tests,
and bounded rapid real route/circuit navigation over shared host history.

## C5 — Harness corrections before spending or running the final campaign

Close the known file-journey harness problems first with an external-model-scripted control
through the actual production UI/runtime/owners. It must cover delayed registration of the
exact approval, app shutdown while evidence remains readable, repeated cleanup, timeout,
quota refusal and queued provider completion. A no-send rehearsal alone does not test this.

Apply WCL-V1: before a test approves a live mutation, validate its actual typed arguments,
original run/proposal/context and exact fixture target, not only its tool name. For file
write validate the unique managed relative path, content/overwrite policy and allowed scope;
for attachment validate project, parent node and expected source path/media. Reject and record
unexpected proposals instead of granting them or repairing production permissions to fit them.

Keep proxy reservations, actual outbound attempts, HTTP status, provider terminal status,
tool batches and owner effects separate. The existing batch watchdog and actual request
budget are different controls. Record which one stops the host. Stop the owned app before
collecting evidence when necessary, but retain the original DB/root until safe read-back;
never recreate deleted evidence and call it original. Use unique per-attempt artifact roots.

For WC-L2 preserve MaxOutputTokens forwarding and reject incomplete Responses even after
HTTP success. Obtain allowlisted safe completion/error metadata through existing contracts
or a test-only external observer, not request/response/credential dumps. Reproduce known
protocol statuses deterministically. The prior 150-token/incomplete explanation is a
hypothesis; do not label it root cause without actual safe evidence. Fixture settings must
be compatible with the configured model and remain explicitly bounded.

## C6 — Frozen broad and composed-application proof

After the repairs and exact dependency selection stabilize, freeze a source manifest across
all changed repositories and build every affected production/test root. Run a fresh documented
full Stable checkpoint, not just the narrow suites. This is explicitly justified by shared
lifetime/owner/Components changes and requested closure. Reconcile discovery and executed
theory cases. Keep all original failures and unavailable lanes distinct.

Run the complete current non-quarantined, non-live browser inventory with unchanged full
server-log/circuit/asset assertions and a fresh owned host. Also repeat a bounded shared-host
route/overlay/close/reopen sequence, with deliberately held reads and fixed reproducible
ordering. Isolated TestLab/Collaboration passes do not close a shared-host failure. If a test
legitimately uses its own host, keep that boundary but retain the composed stress sequence.

Carry all original 29 campaign obligations forward. The six additional closure groups in
`closure-plan.json` do not replace them. Exercise real Defaults/Secrets/Files/History, API,
Storage/selection/Recovery, Data Sources A/B restart and per-group transfer, configuration
trust, Agent and Simple Chat/SSE, Project Structure/file authority, Workflow/Quartz, Resources,
Prompt composer/version, Collaboration, TestLab, CRM/HR, Plugins and Memory according to
VALIDATION_MATRIX. Do not create missing provider features or a test runner in TestLab.

Assess blocked shared-provider/Ollama/image/history-relay/Scenario04 cases individually.
A prerequisite missing in the last run is not automatically still missing; try the existing
safe setup. Do not use retained customer profiles or spend on model downloads/cloud services
without permission. A passed gated no-op remains NOT_RUN in the semantic inventory.

Run current portability-static, source/evidence secret review, maintained documentation,
source/package/asset and protected dependency checks. Changed public/source ownership requires
consumer tests; moving work to a sibling does not exempt it from that sibling's gates.
A later correction invalidates affected evidence. Shared-owner/fixture/graph changes after
freeze require a new affected broad checkpoint; a private test observation fix is recorded
separately and never rewrites the earlier run as all-green.

## C7 — Newly authorized live proof, or explicit authorization blocker

The previous campaign used all forty reservations. This bundle does not authorize another
request by itself. Do not truncate/copy-to-empty/delete the old journal, change 10/40 constants,
or create new proxy execution IDs to evade a limit. Honor an explicit new operator instruction
or independently supplied authorization record, bound to this closure and an actual provider.
Use the template only as a form; Codex must not mark it authorized on the operator's behalf.

When authorized, run the actual positive Project Structure file/attachment/two-read-back/UI
reopen path and its negative controls, plus the actual Workflow-to-project-asset path and
impacted CRM/HR live controls. Keep the original limits or any stricter authorized bounds;
use a single fresh, separately identified journal across attempts. No hidden extra cost/token
increase. Failed attempts consume reservations. Budget refusal is not provider malfunction.

When no new authorization exists, finish all non-live closure work and mark the required live
rows BLOCKED_AUTHORIZATION in their reason. Do not ask repeatedly or stop at this step alone.
Do not issue a global readiness verdict while required live proof remains incomplete. The
operator can then authorize the narrowly specified remaining live run without another code
refactor. A reproducible production defect receives a deterministic regression before repair;
model noncompliance or unsupported fixture parameters are not proof of a UI regression.

## C8 — Close findings, delivery and readiness

Update maintained findings, campaign report and Workspace census with exact implementation
commits, original reproduction, corrected controls, real semantic result and remaining limits.
Do not erase historical attempts or relabel source hashes as commits. Keep structural
Workspace completion separate from behavioral closure, dependency delivery and environment/live
coverage. No new module is part of this run.

For small additional reproduced defects, repair locally and rerun owning consumers. For a
new complex authority/schema/transaction/multi-owner issue, stop the unsafe lane, record its
causal map, original scope/effects and proposed repair slices, continue independent safe
verification, and leave readiness false. Mapping a known WC-C1/C2/C3 in this assignment is
not completion: they are now authorized repair work, not deferred investigation by default.

Local commits may be made when current repository rules permit. Preserve GPG identity,
signing and the already authorized terminal/session. Never disable signing to finish.
No push, merge, PR, deployment, destructive reset or ordinary-host restart is requested.
Release only owned resources after evidence capture; report exact residual artifacts.

Final report must separately state: Workspace rendering complete; WCL-R1 and WC-C1/C2/C3
closed or open; harness safe; new Stable/browser checkpoint; each required live/env result;
Components delivery state; static/secrets/docs; and `ready_for_next_module` with explicit
blocking reasons. Run this handoff's structural/evidence validators, but never present their
success as application execution. Do not end with a plan for a new module.
