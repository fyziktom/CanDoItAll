# Execute: API lifetime corrections, then Workspace Storage Catalog UI

You are Codex GPT-6 Astra Max acting as the senior C#/.NET and Blazor engineer for CanDoItAll.
Implement this assignment, not only a plan. First reproduce/repair AP-R1 and AP-R2, then
complete the bounded Storage catalog administration extraction and independent sandbox.
Do not stop after the corrections. Do not start placement recovery, catalog picker, Data
Sources, Projects, Workbench or Processes extraction in this run.

The purpose is a smaller safe dotnet-watch UI development loop, not API-only transport or a
new Storage runtime. Workspace is deliberately split into smaller slices. Do not reconnect
its independent leaves through broad shared contracts or a universal Settings controller.
Use engineering judgment for class/project boundaries; acceptance is behavioral/architectural,
not an interface quota or strict scripted sequence.

## 1. Establish current context and preserve the input

Review origin: branch `components-decoupling`, implementation
`56f615a19f5d7eb22042584230ceebcc611bf549` (API Access and Files target identity). Its predecessor
archives the last handoff. The user's repeated Memory wording does not authorize repeating
Memory work. Work on the actual owner-assigned current checkout; record entry HEAD/dirty state
and sibling revisions. Never checkout/reset to a review SHA. Reconcile genuine source drift.

Before changing source/build/tests, read current AGENTS.md, .github/copilot-instructions.md,
docs/architecture/ui-component-seams.md, complete docs/testing.md, current CI and applicable
CanDoItAll.SharedInfo standards. Read this complete package:
[review](API_REVIEW.md), [Workspace status](WORKSPACE_STATUS.md), [architecture](ARCHITECTURE.md),
[Storage review](STORAGE_REVIEW_NOTES.md), [sensitive/effect rules](SENSITIVE_STATE.md),
[acceptance](VALIDATION_MATRIX.md), [application regressions](APPLICATION_REGRESSION_MATRIX.md),
[development-loop proof](DEV_LOOP.md), [review limits](PROOF_STATUS.md), [sources](SOURCES.md)
and [shared foundation](shared/README.md). Shared historical module maps are not today's queue.

Use available CodeAnalytics/Components/dotnetwatch MCP tools to inspect consumers/components;
actually try relevant tools, never invent results. When not callable, record it and use local
source, current project evaluation and real CLI/browser evidence. Preserve this archive as
historical input; put implementation/validation notes in maintained repository documentation.

## 2. S0 — two related API lifetime defects

AP-R1: ApiPageController owns its CTS with using, but clears its retained reading reference
only when it may still publish. Current denial or external authority retirement can make that
predicate false. After the read returns, Dispose/Invalidate can Cancel a disposed CTS. Repair
resource ownership independently of publication, with exact reference/generation checks so
old completion never clears a successor. Cover repeated disposal and adjacent ApiAccessSession
retained-read handling where demonstrably affected. Do not fix by globally swallowing disposal
exceptions, leaking sources, removing cancellation or suppressing browser errors.

AP-R2: account and token-list pages receive their child lifetime. Current UnauthorizedAccess
retires that child, not the session's shared administration authority; a sibling issuance
controller/disclosure can remain live. Separate close/cancellation of a view from current
management-denial propagation. Current denial must retire the correct authority and sensitive
children; stale/closed/prior-activation denial must not touch successors. Do not make ordinary
dialog closure revoke all management. Preserve original admitted safe receipts and no-replay
rules. No backend policy or token revocation changes are required.

Start with deterministic failing-first current-denial and retirement-order tests, then actual
renderers and production host. Cover account list, token list, page correction, sensitive
sibling fields, retry status/access, tab-away and close. Existing stale A→B→A/observation tests
must remain. Keep Files target revision correction, Resources readiness, Memory result origin
and Core sensitive-state behavior. If current code already solves a finding, demonstrate it
instead of rewriting working code. Continue into the selected Storage slice.

## 3. C1 — inventory and baseline before extracting

Inventory the whole StorageSettingsPanel tree: list/search/summary, three-step editor,
FileSystem/IPFS/FTP settings, references, flags/order, default purposes, Test/Save/Delete,
notices and Recovery launch. Trace WorkspaceService.Storage into StorageCatalogService,
per-purpose routing writes, credential resolution, driver test, host-binding/bootstrap and
Activity. Identify actual picker/Resources/Agent/Workflow/attachment consumers and relevant
current tests. The shared parent route does not make all of them part of this extraction.

Capture a real production wizard baseline in a private environment, including system default,
normal custom catalog, all steps, reference/error states and passive Recovery open/close.
Record original Web and protected Core/API sandbox evaluated graphs/watch sets. Do not use
operator storage, live FTP/IPFS accounts or the app on port 5032.

Read ST06's real persistence tests before altering owner methods. Preserve exact configuration
bytes when configuration is omitted; explicit wizard configuration replacement remains explicit.
Malformed unrelated provider/alternative JSON must not break metadata-only queries or be
silently normalized away. Do not merge the different saved-row connection-test service into
the draft-based Workspace Test path solely for code reuse.

## 4. C2 — dedicated leaf with no new cross-module coupling

Recommended locations:

```text
src/Modules/CanDoItAll.Modules.Workspace.StorageCatalog.Contracts
src/UI/CanDoItAll.Workspace.StorageCatalog.UI
src/Sandboxes/CanDoItAll.Workspace.StorageCatalog.UiSandbox
```

Reuse an equivalent genuinely light existing boundary when justified. A presentation project
is optional. Separate cohesive state/reads/editor/effects; do not create a service bag, ambient
IServiceProvider, universal manager or new partial service cluster.

Core UI/contracts/presentation/sandbox and API leaf/sandbox must not acquire Storage references.
Infrastructure must not reference product Workspace contracts. The production Workspace module
composes the new leaf through its current active slot. Use narrow safe UI projections for the
Infra-backed storage models rather than moving the entire Storage API and every consumer.
Preserve non-UI contracts, enum/flag meaning, namespaces/wire shapes where relied upon.

Production owns profile identity, actual driver/template/capability policy, serialization,
bootstrap and host binding, secret runtime resolution, routing and persistence. Do not expose
DbContext, StorageDriverInput, grant/session objects, actual secret values or runtime DI to UI.
Project canonical choices and safe labels instead of implementing a second routing/capability
engine. Use actual BaseLib Steps/StorageSummaryCard/inputs/dialogs; ask the shared component
owner for a genuinely missing reusable contract rather than copying implementation.

Keep Recovery outside the leaf: a typed event/callback carries the exact persisted StorageId
or explicit catalog-wide null to the production host, which opens its original dialog. Capture
the target when opening. Do not bind an existing recovery to later editor selection. Preserve
close semantics and original recovery services. The independent sandbox labels this as a
deferred host action; it does not load runtime Recovery behind a placeholder wrapper.
Shared Storage catalog field/dialog and generic Settings renderer hosts remain with current
owners. Do not change Agent allowlists, picker AllowAll or global module references.

## 5. C3 — complete safe catalog behavior

### Reads, editor and input

Keep catalog/reference readiness separate from exact editor acquisition. An exact missing
GetStorage result must not become an editable new filesystem template. New is explicit.
Failed exact load can retry same ID. A→B→A or stale success/error/finally cannot replace current
selection, clear a successor spinner, expose the wrong profile, or enable placeholder writes.

Use stable draft/EditContext and immediate text capture, including unfinished numeric input.
Step changes and harmless refresh keep draft/validation/focus. Keep missing references visible
as exact IDs; do not select the first secret/provider. Pure capability preview is not proof of
a successful test. Never call DB/network/secret owners from a render getter or keypress.

Capture complete deep immutable submission values before the first incomplete await, including
DefaultPurposes, provider/configuration/secret, flags, target ID and original profile context.
Per-field/destination revisions preserve newer edits and edit-away-and-back; do not reset the
whole editor on read-back. Confirmed ID enters the matching original receipt/draft before any
secondary read. New/different target cannot inherit it. Preserve the current wizard step.

Mutation admission belongs inside handlers and protects exact target/create origin. Treat Test
as an effect, not a read. Different catalog IDs can still compete for a shared default purpose;
handle that scope consistently within this view without claiming a new distributed lock or
adding a cross-module concurrency framework. Independent safe reads can remain independent.

### Exact mutation semantics and outcomes

Save currently persists catalog, then applies tracked purposes in separate writes, then
records Activity. Preserve those owner semantics; do not silently make them atomic. Expose
truthful typed facts at the real owner boundary: acknowledged catalog ID, routing completed
or potentially partial, Activity and read-back stage. A failed routing call is not proof of
zero routing changes. A generated ID is not a returned durable ID. General exceptions after
known commit cannot erase the known stage or turn it into Unknown. Protect diagnostics too.

Prevent missing existing editor targets becoming new catalog rows. If necessary introduce the
smallest owner-local editor entry point enforcing exact existence at the actual write boundary,
while retaining legacy upsert semantics for non-UI consumers. A separate pre-read alone does
not establish this guarantee. No schema migration or broad concurrency/version protocol.

Known refusal, acknowledged full/partial commit and genuinely unknown acknowledgement stay
distinct. Unknown/partial effects get safe original-target observations and no blind replay.
Refresh/review cannot repeat catalog Save, ApplyDefaultPurposes or network Test. An operator's
subsequent explicit correction is separate, never labelled as a read retry. Keep bounded
receipts without evicting unresolved operations to make room.

### Connection test must preserve its shipped contract

Unsaved draft Test does not create a catalog entry; existing draft Test persists the tested
configuration plus health/capabilities. Preserve and clearly describe that distinction. Do not
turn it into a read-only probe or silently substitute the saved-row health service.

Driver request, secret consumer, metadata write and Activity must retain original target and
profile across awaits. Health/test completion is attached to the tested configuration, not
newer edited endpoint/provider values. Driver success does not certify metadata persistence.
Known health persistence followed by Activity failure remains known; ambiguous persistence
stays unknown. Refresh never reruns the driver. Cover concurrent selection/New/provider edits,
missing credentials/driver, degraded/unavailable result and view/profile retirement.

Use current StorageCredential purpose/consumer policy and private ephemeral owner context.
No credential values in UI/contracts/receipts/logs/scenarios, no provider exceptions copied
verbatim when they may disclose secrets. No new FTP/IPFS capability or transport is requested.

### Preserve general owner behavior

Keep bootstrap/system-managed records protected by actual owners. Existing List/Get/Rules
can ensure bootstrap rows/rules and migrations; document that rather than claiming all reads
are write-free. Do not remove owner initialization or invoke it more widely to satisfy a test.
Preserve existing host binding, exact config serialization, routing priorities/alternatives,
project-specific rules, and file/project authorization. Workspace default unselection must
not disable another storage's rule or discard unrelated fields.

Pin the actual original database/profile/generation through current canonical factories and
fences. Validate those lifetimes instead of assuming a new global service. No later stage of
a held command may land in a newly activated database. Retired reads cannot republish old
state; accepted writes retain their original known facts. API users/tokens stay instance-local.

## 6. C4 — independent faithful sandbox

Use the exact shipped renderer and shared presentation over bounded deterministic owners.
Include realistic/empty/large catalog, system-managed default, FileSystem/IPFS/FTP forms,
missing secrets/catalog, invalid raw numeric input, failed reads and partial references,
refused/unknown/committed-partial writes, routing failure, held driver/test/persistence/readback,
New/selection and reset. Implement real mutable metadata/routing in the original scenario store,
not only a toast for an intended result. Retired accepted work must not write to the next store.
Bound retained stores/receipts/gates; make delayed completions usable while wizard/overlays are open.

No DB, vault, actual remote driver, filesystem catalog owner, production composition, API leaf
or recovery implementation is needed to render. Label simulated remote health and host-only
Recovery. Prove real Steps/inputs/summary/footer layout and assets, not static stand-ins.
Follow existing theme content-link conventions without a Web ProjectReference. Publish and
run its DLL in Production independently.

## 7. C5 — verify application behavior, not only isolated rendering

Execute VALIDATION_MATRIX.md and APPLICATION_REGRESSION_MATRIX.md. Inventory exact owning
tests and counts first; run build-backed discovery, then the identical filtered execution.
Do not reuse stale assemblies or run simultaneous builds into the same configuration. Update
actual product/test/CI membership appropriately; no tests in the product solution. Never
mark skipped/zero/wrong-discovery/failing tests as passed.

Proof layers: deterministic state, actual renderer events, real PostgreSQL Storage owner stages,
private filesystem/credential boundary, production Settings route, actual API denial/retry,
independent source/published sandbox, evaluated/runtime/public contract graph and protected
Core/API graph regression. See the application matrix for routing, attachments, Resources,
Agent catalog restrictions, workflow consumers, Data Sources and passive recovery coverage.

Reproduce catalog/routing/Activity partial failures at the actual owner, not a fake returning
expected receipts. Recheck original row IDs/counts, purposes and file content after operations.
Use real owner test fixtures for storage/secret policy; no allow-all registration in production.
Remote live accounts or actual executable launch are unnecessary. Database-switch tests operate
only task-owned profiles/roots and preserve existing activation policy.

Measure original/final Web and the Storage sandbox plus Core/API containment per DEV_LOOP.md.
Report sample conditions and graph/watch differences; no promised full-Web speedup. Inspect
current supported desktop screenshots, focus/caret, Steps, scroll/footers and recovery overlay.
No unhandled Blazor circuit error, HTTP asset error or page console exception is accepted as
normal. Capture only redacted safe evidence.

Run mandatory portability-static with current protected scope: review added/stale findings,
fix genuine defects, refresh intentional baseline only with inspected diff, then require
no-write enforcement. Run maintained docs/evidence and secret checks. Record a concrete broad
Stable trigger decision after actual public/owner changes are known; honor it once if triggered.
Do not run the entire suite at every phase, or call an old mixed run a clean baseline.

## 8. Scope, signing and final delivery

All code comments, UI and maintained docs are English. Follow current style and versions.
Keep sibling sources/settings intact unless an indispensable shared-component contract is
missing; justify and separately test any bounded owner change. No shared standards rewrite.

Ordinary port 5032, its database/configuration, vault and storage are out of bounds. All test
resources are task-owned and verified by exact identity before cleanup. No arbitrary process
kill, Docker prune, retained-data migration, external account change or deployment. Preserve
GPG configuration and reuse the existing unlocked signing session; never disable signing or
replace keys. Local signed commits are allowed; no push/PR/merge/rebase/reset is requested.

Report entry/final SHAs and worktree/sibling state; AP-R1/AP-R2 failing-first/final proof;
completed wizard and deferred surfaces; dependency/consumer effects; actual owner changes;
exact test counts/filters and initial failures; browser/publish/watch/static/secret evidence;
owned cleanup and unresolved limits. Update the maintained Workspace completion ledger to say
catalog administration is extracted, while recovery, picker family, Data Sources and residual
settings-renderer integration still need their own audit. Do not claim all Workspace finished.
