# Scheduler closure, then complete Memory UI decoupling

You are the senior C#/.NET and Blazor implementation architect for CanDoItAll.
Work on the current checkout. Implement, validate and document this assignment;
do not stop at a plan. The deliverable is a working production boundary and a
representative backend-free development sandbox, not a directory rearrangement.

## 1. Authority, provenance and execution

Read the current `AGENTS.md`, `.github/copilot-instructions.md`,
`docs/architecture/ui-component-seams.md`, the complete `docs/testing.md`, relevant
project instructions and actual CI selections before changing source. Use available
CanDoItAll shared skills and Code Analytics/Components MCP tools; when unavailable,
record that limitation and inspect the real source, contracts, graph and test
discovery. Do not invent MCP results. The repository's current policy is the
architecture authority, with this module assignment and its
[shared foundation](shared/README.md) as the execution companion. [EV06–EV09]

Read [Scheduler review](SCHEDULER_REVIEW.md), [Memory review](MEMORY_REVIEW_NOTES.md),
[validation matrix](VALIDATION_MATRIX.md) and [development loop](DEV_LOOP.md).
Consult the [source register](SOURCES.md) for exact paths and review coverage.

The reviewed implementation is `14f07bffa30ddb124011869e38bc5b264b8bce42`.
`b55baa3a94ff216353768f0a4ac9a50e89463d08` is its child on
`components-decoupling` and only archives the previous Scheduler bundle. Do not
mistake the archival commit for the implementation, count historical instructions
as unresolved code, or rerun the preceding extraction. Record the actual branch,
HEAD, dirty files and sibling revisions. Neither review SHA authorizes a checkout,
reset, rebase, merge or revision pin. Refresh affected source if it has drifted. [EV01–EV03]

Keep code, comments, UI text and maintained documentation in English. Follow the
repository's C# formatting and XML-documentation policy. Keep responsibilities
cohesive; do not satisfy this brief with arbitrary file/line/interface quotas,
partial-class splits, a service locator or a general event/effect framework.

Signed local commits are authorized. Preserve the user's existing Git/GPG identity
and signing configuration. Reuse the established signing agent/session; never
persist a passphrase, disable signing or change trust/security settings to proceed.
Do not push, merge, deploy, restart the ordinary app, or change sibling repositories
without separate authorization. A signing/environment blocker must be reported
honestly; it must not cause unsigned commits or deletion of unrelated changes.

## 2. S0 — close the bounded Scheduler findings first

Preserve the established Contracts/UI/Presentation boundary, real calendar,
production authority and profile guards, exact version/input semantics and owner
commit facts. The preceding Plugins fixes are present and should not be rewritten.
This is not another general Scheduler audit. [PL01–PL03, SC01–SC09]

### SC-R1: give unknown-outcome review its own operation lifetime

`SchedulerMutations.ReviewUnknownAsync` checks the receipt and stored submission
before awaiting an exact-plan read, but unconditionally changes the draft receipt,
plan receipt and submission dictionary afterwards. Two overlapping reviews can
therefore resolve the old Unknown, allow a new Save to enter, and then overwrite
that new Pending state with the old review result. The obsolete review can also
remove the new submission, preventing correct recovery if that new write becomes
Unknown. `ReviewPlanAsync` checks its success publication but not all early/error
publications. [SC02, SC10]

Write a deterministic failing-first test using an existing plan A, one original
Unknown save and two independently controlled exact-ID review reads. Finish the
newer review, start and hold a new save of the same draft/plan, then finish the
older review. The new operation must remain Pending and retain its own submission;
a direct duplicate Save/Pause/Delete must remain refused. Also cover the newer
save becoming Unknown and still being recoverable. Fence stale success, mismatch,
failure and completion, and disposal. Include the corresponding non-save review
path so an obsolete flag/deletion review cannot replace a successor receipt.

Choose a small consistent policy: serialize review per original receipt, or allow
latest-request-wins with identity fencing. In either case handler admission and
post-await identity checks are required, not just a disabled button. Retain the
explicit exact-ID operator review and the distinction between visible persisted
state and proof of the earlier command/Quartz projection. Do not introduce durable
idempotency or reinterpret a name match as recovery. Make the actual review control
reflect its current request, and verify it through the real component/browser path.

### SC-R2: retire validation belonging to a removed dependent input

`SchedulerInputSession.InputAsync` clears dependent values/JSON properties after a
parent change, but only clears validation for the parent itself. An old dependent
error remains and `SaveAsync` refuses before authoritative validation can run. [SC02, SC03]

Reproduce an optional `project -> node` field: fail validation of the old node,
change project so the node is explicitly cleared, then submit. Invalidate only
obsolete dependent diagnostics tied to the removed value/context. Required fields
must still fail fresh validation when absent; unrelated invalid fields and malformed
raw JSON must stay visible and block writes. Preserve incomplete raw input, exact
schema/version identity and the existing targeted option-query budget. A blanket
`Issues.Clear()` or bypass of owner validation is not an acceptable fix.

Complete current targeted Scheduler tests, real control checks and the applicable
static gate, record the S0 result, and proceed to Memory in the same assignment.
If a reviewed path has already changed, prove its current equivalent and avoid
unnecessary edits. A genuine environment blocker is not a pass: continue independent
implementation work safely and report which proof remains blocked.

## 3. M1 — map the complete Memory slice and establish its baseline

The next scope is the **entire existing `/memory` workspace**:

| Surface | Preserve and extract |
| --- | --- |
| Providers | Summary/list/selection, current profile editor, HTTP/NativeRemote/MCP settings, capabilities, tags, health/fallback, explicit demo action |
| Operations | Ledger rows, accepted operation/feedback facts, explicit supported status refresh and existing cancellation refusal |
| Events | Pending inbox/status and existing acknowledgement availability/refusal |
| Feedback | Ledger, matching/unmatched status and existing submission availability/refusal |
| Query | Sync/async choice where supported, provenance input, accepted result/context pack, feedback context |
| Ingestion | Existing editor/availability/result surfaces; no new executable driver feature |
| Provider UI | Registered RCL, safe iframe/external URL and all blocked/missing states |

Inventory all meaningful descendants, `_Imports`, page-scoped CSS, static assets,
service/model references, registry types, existing tests and public consumers.
The controller is already a transient host adapter over an in-process application
facade; reuse its owners rather than inventing a second backend. [ME01–ME03, ME17]

Use a task-owned baseline host and the same build/source-reference mode as the
post-change measurement. Separate the completed S0 change from the Memory baseline.
Record actual graph/watch inputs and representative existing page behavior before
moving renderers. Do not require all test assemblies for this entry step.

The review mapped the core route/controller/owners and key components, not every
Memory driver implementation. Complete local dependency and consumer discovery;
do not treat this source list as an exhaustive affected-test list. [ME01–ME22]

## 4. M2 — establish the light dependency boundary

Suggested locations, not mandatory names when equivalents already exist:

```text
src/Modules/CanDoItAll.Modules.Memory.Contracts
src/UI/CanDoItAll.Memory.UI
src/Sandboxes/CanDoItAll.Memory.UiSandbox
```

A small reusable presentation project is justified only when production and sandbox
share actual state/read/submission policy through it. Do not duplicate a simplified
controller in the sandbox or add a project that merely forwards every call.

The routed module retains concrete application services, provider/ledger stores,
requesters, current profile guards, executable capability enforcement, driver
resolution, production registry composition and side effects. The renderer owns
DOM and typed events/view contracts. Native and remote transports, operation
handlers, workers, EF, Web/composition and source/provider execution must not be
required to build or run its base sandbox. Keep in-process production calls; no
HTTP-only conversion is requested.

Map the actual public type graph. In particular:

* `MemoryProviderProfileEditorModel` imports HTTP/MCP implementations for default
  constants and has a static mapper dependency. Its nested transport models and
  preserved manifest metadata must become genuinely data-only or be mapped at the
  owner boundary. Do not pull the drivers into the renderer for a default string.
* UI result records currently use Application-owned statuses as well as abstract
  provider values. `Memory.Application` does not directly reference EF, but is an
  application implementation, not automatically a UI contract. Prefer a bounded
  exhaustive display projection or a small genuinely shared value relocation.
  Do not undertake a global runtime decomposition just to reuse one enum.
* Reuse existing `Memory.Abstractions` where its evaluated closure is appropriate.
  Do not clone all protocol types or turn meaningful operation outcomes into a
  Boolean success. Preserve numeric/default/JSON behavior of public consumers.
* Preserve provider profile round trips, including unknown vendor JSON, capability
  and UI surface metadata, limits, protocol/interaction support, selection tags
  and credential-reference semantics. Snapshot nested collections and JsonElements
  safely; avoid per-render serialization of the complete profile.

Keep API/configuration behavior compatible if models move. Add explicit source or
binary forwarding only where an actual public consumer requires it. Do not expose
an infrastructure store, an arbitrary `IServiceProvider` or a runtime object through
a neutral-looking DTO. Evaluate negative direct/transitive/public-type guards. [ME10, ME14, ME15, ME19, ME20]

### Registered provider UI is an explicit extension boundary

The current host chooses a registered component key, or a URL projected by host
policy. Preserve enabled/healthy/declared-capability checks and exact registered
component resolution. Preserve supported component parameter contracts and built-in
mock panel behavior. Never load an arbitrary assembly/type string from a manifest.
A typed approved projection or explicit host slot is acceptable; neither may hide
a mandatory production implementation in the base sandbox. [ME07, ME08, ME18]

Keep HTTPS/loopback-HTTP restrictions and rejection of credentials, query and
fragment in provider UI URLs. Do not loosen iframe sandbox/referrer policy or
external link protections to make a fixture render. Use harmless owned local
fixtures for browser proof. Test selection and disposal across mounted RCL/iframe
surfaces; stale A content must not become the current surface of B. Report the
base feature graph separately from optional third-party extension dependencies.

## 5. M3 — preserve draft, request and effect identity

Do not copy the current controller's unguarded asynchronous transitions.
`RefreshCoreAsync` replaces the provider editor after every action. Late queries,
saves and action refreshes can overwrite newer selection, result or tab state;
busy booleans do not provide admission. [ME02, ME04]

Make the policy explicit and testable:

**Drafts.** Use one host-owned origin per provider/destination/editor lifetime.
Keep profile, query, feedback and ingestion drafts distinct. Same-target refresh,
tab change and an unrelated operation must preserve current raw input, nested
transport fields, validation and any actual EditContext. Capture input before blur;
a framework-level Change-only test is not sufficient. An explicit switch/reset may
retire or retain a draft according to a documented bounded policy, but must never
silently drop dirty content on automatic refresh.

**Identifiers.** Provider IDs have existing ordinal semantics. The profile editor
allows editing InstanceId; an upsert under a different ID is not a rename of the
old provider. Preserve that behavior explicitly, capture destination before awaits,
and do not redirect a late save to the currently selected ID. Distinguish initial
no-selection defaulting from an explicitly selected provider that disappeared.
The latter must not silently borrow the first provider for a mutation or result.

**Requests.** Deep-capture complete profile/query/provenance/feedback/ingestion
inputs before the first validation, guard, authority or transport await. The
current query and feedback services read mutable editor fields after guard awaits;
keep the boundary safe by giving those services a detached immutable/copy input,
or by a bounded owner-entry snapshot. No contradictory capability decision from
an old checkbox and payload from a new edit. Avoid locks held across remote calls.

**Reads.** Fence success, error, completion and disposal by the exact request,
selection, draft and database-profile origin. Cover A -> B -> A, refresh while
selecting, independent ledger failures, missing profiles, same-ID replacement and
newer action results. One older finally cannot clear another operation's busy state.
Current profile-scoped owners remain authoritative; never infer authority from UI
selection. No global cross-circuit mutable cache.

**Write admission.** Enforce at the handler/owner seam, not just visual disabled
state. A duplicate Save or query/action while its same-origin operation is admitted
must not dispatch twice. Independent targets may progress when the backend supports
it. Superseding the view cancels only owned reads/effects; it is not evidence that
an already dispatched provider operation or persisted job did not happen.

**Accepted outcomes.** Preserve exact saved profile identity and existing operation,
job, snapshot, accepted-operation, feedback-handle and dispatch-attempt facts.
Known refusal before dispatch, accepted asynchronous work, terminal output,
committed-with-refresh-warning and genuinely unknown dispatch are different.
A read-back error must not erase a returned identity or invite replay. A query is
not assumed side-effect-free merely because its name is Query: it may create
operation/feedback ledger state and contact a provider. [ME05, ME06, ME12–ME14]

The page-wide Refresh is a read action over owned snapshots. The row's explicit
operation-status action may call its supported provider status port and update a
ledger; keep these actions distinct. Never replay a query, status request, enqueue,
feedback or acknowledgement automatically to obtain a clean snapshot.

Use existing owner receipts when sufficient. Only add a narrow typed observation
at a real affected commit/acceptance boundary when required to preserve a fact.
Do not reclassify every exception as either refusal or success. The demo action
currently creates up to two profiles sequentially: preserve observed partial
success without implying a transaction or recreating confirmed profiles blindly.
There is no new distributed idempotency protocol in this task.

## 6. Shipped capability limits are non-negotiable

At the reviewed code, the policy is:

| Driver | Executable action subset relevant here |
| --- | --- |
| Mock | Synchronous query; approved UI surfaces |
| Http / NativeRemote | Synchronous query; approved UI surfaces |
| Mcp | Synchronous/asynchronous query and operation status, with the required configured tools; approved UI surfaces |
| All current drivers | No operation cancellation, manual ingestion, feedback execution or provider push acknowledgement |

Do not infer support from a manifest flag alone. The real executable guard still
checks the current stored profile, health/enabled state, claim and shipped driver
policy. Unsupported imported claims remain denied. [ME09, ME11, ME21]

Keep all seven tabs and meaningful unavailable/ledger views. For currently refused
commands the required production proof is **refusal before dispatch**, not a fake
success. Do not add drivers, start disabled workers, change feature defaults, grant
capabilities or weaken policy merely to satisfy a test.

The dormant ingestion implementation records enqueue identity before a ledger
lookup, but is currently denied by the shipped policy. This is a boundary to
preserve if touched, not authority to enable ingestion or a claim that the reviewer
reproduced an accessible production failure. Synthetic accepted-result fixtures may
exercise rendering only when labelled as such. They are not live capability proof.

Never infer trusted feedback matching from an arbitrary context-pack ID. Preserve
unmatched/unsupported/denied diagnostics and the accepted-operation/feedback handle
semantics already returned. Keep raw legacy credentials out of UI state, logs and
new persistence. Do not read environment-secret values for presentation; preserve
HTTP versus MCP environment-reference meaning and unknown safe extension fields.

## 7. M4 — build the real development sandbox

Build and run the same full workspace and meaningful descendants without production
Memory/module/runtime DI, PostgreSQL, real drivers, vault or external accounts.
Use deterministic local records and controllable waits at the actual owner port.
Share production presentation policy where justified. Fake writes change the
original scenario store and remain readable there after view retirement; do not
just return success labels or redirect an old write into a successor scenario.

Scenarios must cover initial loading, no providers, realistic and bounded large
catalogs, selection/missing provider, unavailable versus stale/partial reads,
dirty profile and nested transport editing, refused/accepted/unknown results,
held query and status read, supported versus unsupported driver claims, operation
and context-pack provenance, and all registered/URL/blocked Provider UI variants.
Use reset/release controls that settle their own pending waits. Rendering synthetic
records for unsupported features must be clearly distinguished from enabling them.

Retain current desktop layout, labels, useful sections, focus and scroll. Move CSS
with the DOM that receives its scope, including child component boundaries; prove
computed styles, not just a 200 response. Keep authoritative theme/static assets
without a Web ProjectReference. Record the base and optional extension render tree,
asset/watch inputs, and published standalone host behavior. No fonts/binaries are
copied into this handoff; consume repository assets normally. [EV09, EV12, EV13]

## 8. M5 — validation and completion

Use [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md), not a guessed historical case
count. Start with targeted current discovery, then execute the identical filter
against newly built assemblies. Use existing tests and public seams; do not replace
owner/persistence tests with an echo fake or delete behavior tests to fit new paths.
The prior Memory responsibility checkpoint may need a placement-aware update, not
an arbitrary class/file quota. Preserve lossless mapping and capability tests.

Required proof includes the light renderer/presentation project; the real page and
DI; actual PostgreSQL profile/ledger ownership; a harmless synchronous query through
the real handler; exact refusal of unsupported actions; controlled MCP async/status
transport where applicable; existing affected API/serialization/configuration and
Agent Memory consumers; sandbox and production Playwright; published assets; and
measured dev-loop behavior. Keep live external accounts separate and never claim
those tested by a loopback fixture.

Database proof uses task-owned PostgreSQL 18 through the documented test settings.
Do not touch the ordinary application or database on port 5032, manual-provider
data, existing watch sessions or unrelated Docker resources. Match resource
ownership before cleanup. Keep credentials private. A missing Docker/SDK/database
is a blocked lane, not permission to substitute EF InMemory for persistence proof.

Build each changed production root and affected consumers. Add the new lightweight
tests to the relevant Components/Stable solutions and actual CI shards, not the
product solution. Re-evaluate direct/transitive references, package/runtime/native
assets and source-mode replacement. Run mandatory portability-static against the
complete proposed tree, review added/stale entries, and require final enforcement
without `--write-baseline`. Run current documentation/evidence/secret checks.
Widen Stable/platform/live tests only for actual named invalidation triggers; explain
the decision. Do not run the entire suite after every phase or leave required proof
vaguely to CI.

Update the canonical module-local READMEs, sandbox inventory, affected testing and
architecture records. Record exact commands, expected/discovered/expanded/executed
counts, failures, skips, source revisions, inspected images, graph/watch samples
and owned-resource cleanup. Distinguish tests observed now from historical reports
and source-derived findings. The prior 245 cases are an implementer receipt, not
this reviewer's rerun and not automatic closure of new work. [EV05, EV08]

Deliver a concise implementation report with S0 resolution, Memory coverage,
remaining supported-product limits, any true blocker, current proof, GPG-signed
local commits and clean/unrelated working-tree status. No push or merge. Finish
Scheduler fixes and this complete Memory slice, then stop before another module.
