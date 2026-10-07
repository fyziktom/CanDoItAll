# Codex assignment: Plugins closure, then complete SchedulerPlanner UI decoupling

You are the senior C# / Blazor architect implementing this assignment in CanDoItAll.
Deliver the working change, affected tests, production and sandbox proof, and maintained
boundary documentation. Do not merely propose an architecture. Choose the simplest
correct design; no prescribed class, interface, project, test or commit count.

## Mission and order

**S0: reproduce and fix the two bounded Plugins review findings. Then S1–S5: extract
and validate the complete existing `/scheduler` workspace. Do not stop at S0.**

The Plugins implementation is substantially accepted. Keep its Contracts / UI /
Presentation split, shared production/scenario policy, exact draft reconciliation,
independent reads and typed owner receipts. Do not replace it with another framework.
The two remaining findings concern recoverability of a missing selection and preservation
of a package-stage outcome when cleanup itself fails; see [PLUGINS_REVIEW.md](PLUGINS_REVIEW.md).

The next module is SchedulerPlanner, not Processes, Workflows, ProjectStructure, the
application shell or a new scheduling engine. Complete the four existing tabs and all
relevant descendants/dialogs, real CanvasCalendar and contextual Scheduler Agent hook.
Keep in-process production service calls. No HTTP round-trip is required for decoupling.

Read this prompt, [source review](SCHEDULER_REVIEW_NOTES.md), [validation matrix](VALIDATION_MATRIX.md),
[development-loop requirements](DEV_LOOP.md), [source register](SOURCES.md) and the
[shared execution brief](shared/prompt.md), architecture and validation documents.
The shared foundation is unchanged; its historical module map is not a new execution order.

## S0 — bounded Plugins corrections

### PL-R1: keep the catalog usable when the selected plugin disappears

At the reviewed HEAD, `PluginWorkspaceReads.CatalogAsync` only initializes a null selection,
while `PluginsWorkspaceSurface` hides the entire list/detail shell when `SelectedPlugin`
no longer resolves. A successful nonempty catalog refresh can strand the page with no
way to choose a remaining plugin. Preserve a selectable catalog independently of a
valid detail target; show a missing-selection/recovery state. Keep retained draft origins,
unknown/pending writes and stale-result fences intact. Do not silently rebind A's editor,
receipt or action to B, and do not require a page reload for routine selection recovery.

Use the real top-level renderer with the production presentation policy and controlled
owner reads: A/B initially, selected A, then B only; empty and subsequent repopulation;
late earlier catalog results; an unresolved/dirty A draft. Prove B can be explicitly
selected without a write, replay, loss of A's retained draft or stale popup.

### PL-R2: cleanup failure must not erase package replacement progress

The extraction catch currently deletes its temporary directory before converting an
already-started installed-directory replacement into `PluginPackageStageException`.
A second I/O/access failure during cleanup can escape as a plain package error, becoming
`Refused` with no progress and unlocking replay after an irreversible step began.

First reproduce the double-fault with deterministic, isolated fault control. Preserve
the primary exception and exact known stage/identity even if best-effort cleanup fails.
`ReplacementStarted` must not become `Installed`; a pre-effect invalid upload may still
be Refused. Verify the real owner/result adapter and presentation replay lock, not only
an exception class. Keep archive/path/size rules and cleanup attempts. Use test-owned
paths and the narrowest useful test seam; do not introduce a generic filesystem framework,
change machine permissions, elevate, delete user files or redesign package installation.

Run the affected current Plugins component/session/owner and browser lanes, with actual
discovery. Keep the prior 160 Plugins/consumer and 21 TestLab cases labeled historical
unless freshly rerun. No new TestLab changes are planned. Once bounded corrections pass,
continue with SchedulerPlanner in this assignment. Do not restart completed modules.

## S1 — establish the actual baseline and rendered closure

The inspected branch was `components-decoupling` at
`0e176a3b99270cdc9a86a57d6276d05979d7352e`. Record the actual starting HEAD, worktree,
branch, sibling revisions, SDK/MSBuild/runtime, dependency mode and isolated configuration.
Do not reset/switch to the review SHA, silently discard local work or treat matching file
names as proof of compatibility. Inspect drift and additional callers at the actual HEAD.

Read current `AGENTS.md`, `.github/copilot-instructions.md`,
`docs/architecture/ui-component-seams.md`, `docs/testing.md` and applicable CI/solution rules.
Use Code Analytics MCP for symbols, consumers and impacted-test selection when available;
corroborate it with source and discovery. If unavailable, say so and use local search and
evaluated build metadata. Do not claim an MCP call or test run you did not perform.

Inventory `/scheduler`, its four tabs, all dialogs and controls, imports/public types,
page-specific CSS and JavaScript, BaseLib/CanvasLib assets, contextual Agent integration,
production DI and public/managed-Agent consumers. Characterize the current real page
before movement with its existing tests and browser. Capture a comparable development-loop
baseline before extraction. Record which owner and profile every read/write belongs to.

The reviewed monolith is `Pages/SchedulerPlannerPage.razor`, with its scoped CSS and
`wwwroot/js/schedulerPlannerCalendarInterop.js`. `SchedulerPlannerModels.cs` mixes pure
records, editor/query data, EF entities/mappings and privileged launch metadata. Do not
move that entire file into a lightweight contracts library. Inspect the rest of Scheduler
services, schema/options, trigger projection, fire admission and agents before owner edits;
the handoff source review is not an exhaustive audit of all runtime implementation.

## S2 — define and implement the smallest complete dependency cut

Suggested project locations, not mandatory names or a quota:

```text
src/Modules/CanDoItAll.Modules.SchedulerPlanner.Contracts
src/UI/CanDoItAll.SchedulerPlanner.UI
src/Sandboxes/CanDoItAll.SchedulerPlanner.UiSandbox
```

Use a small separately shared presentation policy project only if production and sandbox
really need the same logic, as Plugins now demonstrates. Do not duplicate a production
state machine in a misleading fake, and do not create a generic scheduler/plugin UI engine.

Production routing, owner adapters, authority capture, persistence, Quartz hosting/projection,
Workflow launch/recovery and managed Agent execution stay outside the renderers. Renderers
may own browser-local interaction/JS and receive typed view state, intents and explicit
host slots. A cohesive workspace contract or focused presentation records are both valid.
The complete production page must actually use the extracted renderer, not retain a second
private implementation in the module. Descendants and deferred overlays obey the same cut.

Move only required light contracts while preserving existing namespace, enum numeric
values, serialization/defaults and real source/binary consumers where relevant. Use type
forwarding only when justified; do not create parallel competing definitions. Existing
`AgentFramework.Models`, Workflow input descriptors or shared Canvas models may be reused
when their evaluated closure is light. A namespace containing “Framework” is not itself a
forbidden dependency; a transitive runtime, EF, composition or concrete provider edge is.

`SchedulerPlanEditorModel.StructureAuthority` is host/owner authority, not operator-entered
form data. Do not expose it as editable/serializable renderer state, synthesize it in the
sandbox or copy it from a stale selection. Prefer a presentation draft with only editable
fields and a deliberate host mapping to the existing owner command. Keep ignored privileged
fields ignored in current public contracts. Do not give every consumer a dependency on a
new UI/Canvas assembly simply because one legacy workspace DTO combines those concerns.

Preserve the public `/scheduler` route, effective control-plane contracts and production
composition behavior. No new JWT/auth policy, database table, migration, ownership model,
protocol upgrade or Quartz/package-version upgrade is authorized by UI extraction.

### Complete surface obligation

Ship Calendar, Schedules, New schedule and History, the target picker, edit and delete
dialogs, current CRON presets/description, timezone/misfire controls, typed input form,
advanced raw JSON, search/filter/tag controls, history status/route/retry/result display,
calendar selection-to-edit and the real contextual Scheduler Agent affordance. Preserve
existing useful test IDs or update real consumers intentionally. Maintain a compact desktop
composition with existing component families; do not redesign the visual system.

The backend currently lists Workflow targets only. Legacy Process enums/history/synthetic
picker fixtures are not permission to enable production Process scheduling. Correct misleading
copy without removing wire compatibility or advertising an unavailable launch feature.
Preserve invisible-but-persisted editor fields such as start/end bounds during round trips.

## S3 — stable drafts, exact targets, honest results and cheap reads

### Immutable submission and target-owned state

Capture an independent command snapshot of every editable value and exact target identity
before the first validation, authority or owner await. Binding a service call to a mutable
page field is not a snapshot. Distinguish new versus existing plan, draft identity/revision,
plan ID, target kind/ID/version, current profile/context and modal lifetime. Do not resolve
an explicit old Workflow version by silently adopting a newer version or display-name match.

Keep raw unblurred name/description/CRON/timezone/input values and validation state. Maintain
one pending mutation per relevant plan/draft with actual handler-side admission; disabled
markup or the existing global UI generation counter is insufficient. Independent unrelated
targets may progress when safe; opposite pause/resume/delete/save actions on the same plan
must have an explicit conflict policy. Do not add durable exactly-once or concurrency claims
from a local UI lock.

Awaited validation/normalization and authority capture must apply to the submitted snapshot,
not a newly edited or replacement draft. Reconcile returned values only where the user has
not made newer edits. Accept the real saved plan ID before secondary refresh/reset/close.
A default-editor load must not overwrite a later draft. Save, target picker, dialog close,
calendar interaction or Agent completion cannot close/replace a successor or force its tab.
Retirement suppresses stale presentation effects but does not pretend an admitted owner
write or accepted Workflow was cancelled.

Keep errors scoped to their current origin. Initial, empty, missing, unavailable, stale,
pending, known committed-with-warning and genuinely unknown are distinct. Unknown keeps a
safe replay lock and explicit operator review, with exact identity where available. Refresh
is read-only; never infer a create by matching its name in a list. Clearing an old notice or
reopening a dialog is not proof a pending write failed. Preserve receipts needed for recovery
without retaining unlimited drafts, operations or browser objects.

### Owner commit versus Quartz projection

At the reviewed owner, SavePlanAsync persists the plan and then synchronizes the Quartz
projection, reloads and logs. Pause/resume and delete also persist before synchronization.
A sync/readback/logging error does not roll back the already committed plan mutation.

Introduce the smallest typed observed-result/receipt or committed-exception path needed to
report the actual boundary and ID. Keep the existing successful owner API and behavior of
managed Agent/other callers compatible or adapt their directly affected consumers with
owning tests. Only claim commit after the real durable boundary, not a pre-commit callback;
ambiguous commit failures remain unknown. Preserve the current optional transaction and
source-authority lease order rather than wrapping provider execution in another transaction.

A known persisted plan with unavailable projection/readback is “saved, follow-up incomplete”,
not “not saved”; retain its actual ID. Do not create a duplicate to refresh it. Deletion and
pause/resume receipts describe their exact completed fact even if future-trigger projection
is not confirmed. Do not promise no future fire when projection/revocation has not been proved.

Read-only retry must not resubmit a mutation, reauthorize a plan, synchronize indiscriminately,
launch a Workflow or redispatch a historical fire. If an explicit projection-repair action
is necessary for a safe usable recovery path, use the existing owner's exact-plan mechanism,
current state and authority policy; distinguish it from a read refresh and from execution
retry. Do not create general legacy-fire/operator recovery features in this UI assignment.

### Typed input and advanced JSON

The current Save flow invokes SynchronizeWorkflowInputJson before validation; its parse
fallback silently creates a fresh object on a JSON parse error. Replace this data-loss path:
retain malformed/non-object raw input and report validation without manufacturing `{}` or
defaults. Only an explicit reset may discard it. Preserve unknown/unrepresented properties,
supported root-path semantics, types, optional/default behavior and exact schema version.
No new general JSON-schema editor or schema migration is required.

Separate raw input ownership, parsed state, validation results and option lists. Late schema,
validation or option reads cannot patch a different plan/version/draft/revision or restore
cleared dependent values. Distinguish missing options, unavailable services and empty results.
Retain actual selected IDs rather than falling back to the first option. Preserve valid
owner normalization while keeping newer raw/typed edits and incomplete numbers/whitespace.
The existing edit form is raw-JSON based; reuse form components where helpful without making
unrelated new editor features a prerequisite for completing the extraction.

Avoid querying every option source on each input event and again on blur. Use focused
invalidation, coalescing/debounce or bounded scoped caches as appropriate, keyed to exact
source context/target/dependencies. Pure rendering, filtering and receipt painting must not
cause owner I/O. Keep generation fences for stale success/error/finally, including A→B→A,
manual refresh overlap, edit/readback, filter changes, close/reopen and disposal. Own CTS
lifetimes until continuations unwind. No global cross-circuit cache or unobserved task.

Preserve the current query bounds: default history Take=50, owner clamp 1..250; 30-day
planned projection with at most 18 occurrences per enabled plan, bounded combined calendar
at 160 events; recent history overlay is drawn from the bounded queried history. These are
current bounds, not a claim that the calendar represents every run. Do not expand them or
load complete histories as an incidental UI refactor. If revising projection labels, keep
caps/coverage honest. A narrowly justified separate read projection is allowed; a general
paging/analytics/runtime performance redesign is not.

### Time and deletion semantics

Preserve Quartz CRON grammar/presets, exact timezone ID, UTC timestamps, start/end bounds
and all existing misfire-policy meanings. Distinguish schedule timezone from display timezone
and host locale. Reuse the real owner's CRON/occurrence behavior; do not write another parser
in the renderer. Provide deterministic tests around supported zones and daylight-saving
transitions using the installed implementation, not a guessed independent recurrence rule.

Correct the current delete confirmation claiming existing run history is untouched. Current
Plan→display-Run persistence cascades, while retained FireAdmissions have separate lifetime.
Verify the real database behavior and describe exactly which data the action removes or
retains; do not claim actual Workflow runs are deleted without evidence. Keep the existing
schema/cascade and admission retention rather than changing persistence to fit old copy.

## S4 — real Calendar assets and host-owned Agent integration

Use the real shared CanvasCalendar in production and sandbox. Keep view/date/selection and
interaction behavior, all required scripts/style/font/assets, and event-to-plan identity.
Planned events can identify a saved plan; execution-history events are read-only and must
not be guessed into editable plans by a title or unrelated run ID.

Move scoped CSS with the real DOM owner and verify `::deep` selectors after component
boundaries change. Preserve production/published static-web-asset URLs intentionally. Do
not reference Web or a heavy implementation project to obtain CSS/JavaScript; an explicit
authoritative content input with a documented rebuild path is acceptable.

The current calendar JS has one module-global `activeBinding`, installs a document listener
and does not scope events to its `host` argument. Make registration truly host-owned and
idempotent. Two mounted Scheduler surfaces must not steal callbacks or detach each other's
listeners. After tab unmount, remount or delayed import, attach only to a live host and detach
only the owning registration. Dispose DotNet references/modules safely, including disconnected
circuits. Prove actual mouse double-click routes once to the correct event/plan and that late
calendar callbacks do not reopen a retired dialog or override the successor tab. No new
shared calendar engine or blanket sibling component rewrite.

Keep Scheduler Agent discovery, execution and contextual authority in production. A typed
host slot/presentation seam can supply the actual avatar/button and current context without
pulling `IAgentFrameworkWorkspaceService`, agent runtime or full application composition into
the renderer. Preserve the existing managed Scheduler identity, context source/route/facts,
selected plan/target/overlay, completion refresh and Loading/Failed/Ready state. Presentation
access flags are not runtime command admission. Do not broaden permissions to make tests
pass or give the fake workspace local-operator authority. Suppress late launches/notifications
where their originating view is no longer valid. Sandbox records a harmless intent only.

## Domain invariants that this change must not weaken

Keep Workflow-only dispatch and all existing owner authorization. The UI does not acquire
authority by owning a DTO. Capture local-operator authorization at the production boundary
for the exact submitted intent; preserve managed-Agent original catalog lease and governance
ceiling. An Agent sandbox must not adopt current project selection, all-project access or
Structure/task/asset permissions. Historical authority cannot be rewritten by editing a plan.

Retain exact saved Workflow version/input/correlation/authorizer snapshots, generation-fenced
short fire claims, original caller/idempotency/run identities and accepted-run observation.
Current disable/delete/authority-replacement checks remain with Scheduler/Workflow owners,
including transaction-enlisted policy. Do not reorder original Agent source lease after SQL,
run providers inside persistence locks, or make UI reads trigger recovery/execution.
Do not alter retained FireAdmission semantics, transfer/migration ownership or legacy-fire
reconciliation scope. Rebuild/test affected consumers when contracts move; unchanged deep
runtime code is not an invitation to refactor it.

## S5 — representative sandbox and layered proof

The complete sandbox uses the same shipped renderer and, where feasible, the same actual
presentation/session policy, substituting only explicit owner/effect ports. Fake writes
must update bounded scenario storage so later reads verify actual plan IDs, payloads,
statuses and deletion semantics. Clock and controllable waits must be deterministic.
Do not run EF, a database, Quartz hosted scheduler, Workflow runtime, real Agent, OAuth,
provider or installed plugin merely to display sandbox scenarios. CRON descriptions and
projected event fixtures must be labeled scenario data, not live scheduler proof.

Include representative, empty and large bounded workspaces; all four tabs and dialogs;
new/edit targets, missing plan/version/option, validation failures including malformed JSON,
held schema/options/validation/authority/save/readback; successful commit with projection or
read failure; unknown/refused save; later input; pause/resume/delete conflicts; retired
selection/modal; calendar/history statuses and Agent unavailable/loading states. Scenario
reset retires the old lifetime and releases waits without transferring an admitted old
write into the new scenario's store.

Execute [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md), deriving exact test filters/counts
from current source and matching build-backed discovery before running. Do not treat a
zero-discovery filter, skipped theory, test source or one smoke screenshot as execution
proof. Preserve distinct lightweight/session, real owner/PostgreSQL, production route,
Canvas/browser and dependency/asset evidence. Test real owner changes at the durable boundary,
not with a fake returning the desired status string. Use harmless exact Workflow fixtures
and isolated test-owned resources; no live provider account is needed for UI proof.

Directly build new production projects and affected Web/module/consumer projects. Add new
light tests to the actual component/Stable solution and CI selections, not product solutions.
Actual tests may need new focused files rather than continually growing the existing large
page test. Run required static/portability/documentation/evidence checks; review each added
and stale baseline finding rather than bulk-refreshing allowances. Widen to broad Stable,
platform/live/provider and other module lanes when current instructions or actual dependency,
serialization, lifecycle, owner or schema changes trigger them—not merely because a phase
ended. Explain any blocked proof and complete independent work without mislabeling closure.

Measure graph/startup/edit-to-visible per [DEV_LOOP.md](DEV_LOOP.md). JavaScript and scoped
CSS are applicable here. Keep exact source dependency mode, required watch inputs and real
child renderers. Report all samples and limitations; do not promise full-Web acceleration
from the existence of another assembly or quote Plugins timings as Scheduler measurements.

## Delivery, scope and operational constraints

Keep code, UI text, identifiers, comments, tests, prompt and maintained documentation in
English; the owner-facing final answer may be Czech. Update canonical Scheduler/Plugins
boundary records, necessary project/UI/sandbox READMEs, test/CI discovery guidance and the
requirement-to-evidence mapping. Do not make another permanent shared architecture rulebook.

Report actual start/end SHA, S0 conclusions, complete rendered scope, new boundaries and
public-consumer consequences, owner mutation stages, unresolved debt, exact builds/tests and
counts, inspected browser/assets, graph/watch samples and cleanup. Separate historical
receipts from fresh execution and source review from product runtime proof.

Use isolated output configuration (for example `SchedulerUiProof`) and task-owned PostgreSQL,
ports, temporary package directories, hosts and browsers. Do not contact, restart, kill or
reuse the ordinary app/database on port 5032. Stop only resources whose exact process/container
identity and ownership were recorded; restore measurement edits byte for byte. Sibling
repositories remain read-only unless separately authorized.

Local signed commits are permitted on the supplied branch. Preserve the normal unlocked
GPG agent/session for successive commits; do not disable signing, store passphrases or weaken
global trust/cache policy. Do not push, merge, release, publish, reset or silently switch
branches. A checkpoint is not a substitute for required proof.

Finish S0 plus the complete SchedulerPlanner slice. Do not continue to a third module.
No schema migration, new scheduler/fire protocol, new authorization framework, API-only UI,
generic state/effect bus, unlimited audit expansion or unrelated provider/Workflow rewrite.
Necessary narrow owner-stage fixes and affected contract consumers are expressly in scope.
