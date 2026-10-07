# Codex GPT-6 Astra Max — Memory closure, then complete Resources UI decoupling

## Mission and authority

Implement this assignment on the current CanDoItAll checkout. First reproduce and close
ME-R1 from [Memory review](MEMORY_REVIEW.md). Then extract the complete existing Resources
workspace, including Registry **and** Browse, its promotion dialog, governed reopen and
file actions, into a genuinely lightweight rendering boundary and a faithful sandbox.
Do not stop after the Memory correction, after a plan, or after creating empty projects.
Do not begin a third module.

This is UI/build-graph decoupling, not API-only conversion. Production continues to use
its existing in-process owners. Existing Processes/Project Structure HTTP boundaries are
not bypassed. Storage, file authorization, project lifetime admission, connector semantics
and database transactions remain with their current owners.

Review provenance is `components-decoupling` at
`5f7f8329e3e3e30bd1451fc72757e04847579754`. It is **not an execution pin**. The preceding
Scheduler correction is `51648a77e0f0b60c1acae84e2ccc9880e354a062`; older bundle-only
commits are archived instructions, not additional implementation. Inspect the actual
checkout and intervening changes. Do not reset, checkout a historical SHA, cherry-pick an
old extraction, or overwrite another contributor's work to match these notes.

Read current `AGENTS.md`, `.github/copilot-instructions.md`,
`docs/architecture/ui-component-seams.md`, `docs/testing.md`, and the full actual
`.github/workflows/ci.yml` before source changes. Follow applicable current SharedInfo
skills. Read this entire extracted package, especially [Resources notes](RESOURCES_REVIEW_NOTES.md),
[validation matrix](VALIDATION_MATRIX.md), [development loop](DEV_LOOP.md), and
[proof limits](PROOF_STATUS.md), plus [shared v3](shared/README.md) and its architecture
and validation documents. The repository's maintained seam guide is canonical; historical
module maps and previous execution counts in the sealed shared audit are not current gates.

Use ordinary cohesive design, not an interface/DTO/file quota. Select a presentation record
or workspace view contract according to actual behavior. A small shared presentation
project is justified when production and sandbox execute the same state policy, as in
Memory and Scheduler. Do not introduce a universal controller, generic effect bus,
service locator, or partial-file partition masquerading as separation.

All product code, comments, UI, tests, documentation and commit messages are English.
The final conversational delivery may be Czech. Follow repository brace/style rules;
do not add unsolicited XML documentation. Preserve existing public XML comments where
contract moves require them.

## S0 — finish the bounded Memory review first

Scheduler SC-R1 and SC-R2 are present in the reviewed code. Preserve their request/receipt
identity checks, dependent-issue retirement and focused regressions. There is no new
Scheduler rewrite in this assignment.

ME-R1 is a result-origin problem in `MemoryProvidersPageController`, not a new provider
feature. A query may be correctly published against provider A/revision R0, then the
following snapshot read discovers A/R1 or a missing A. `ReadAsync` advances
`selectionVersion`, but the already published `Draft.QueryResult` remains in the current
Query panel beside the new `SelectedProvider`. The existing held-query test covers a
refresh **before** completion, not completion followed by its own changed-revision
read-back. Preserve the old result as evidence, but do not silently present it as a
current result for the replacement profile. [ME01, ME02, ME03, ME04, ME05, ME06, ME07]

Create failing-first deterministic coverage for both temporal orders: an already visible
result followed by a replacing refresh, and replacement while the query is held discovered
only by the query's automatic read-back. Add removal/reappearance, same-revision refresh,
a stale read finishing after a newer result, and current user edits to query/feedback.
Drive the actual Query panel in at least one component test and a representative browser
journey; separately exercise a real production owner snapshot and its revision calculation.

Choose a bounded solution: retain explicit provider/revision/operation origin for current
results and re-evaluate their presentation when publishing snapshots, or move mismatching
results into a clearly historical/unavailable presentation. A result can remain visible
when labelled accurately. Keep its original submission receipt, operation identity,
accepted operation, feedback handle and context provenance. Do not clear all history or
all drafts, fabricate a new profile lifetime, or change driver execution semantics.
Only retire auto-derived feedback context if its origin no longer applies; preserve
manually entered IDs/comments. Distinguish failed reads from a proven profile replacement.
Refresh and review must perform no query replay or new provider dispatch.

If current code already closes a scenario, prove it and avoid a redundant edit. Record
S0 results, then continue directly to Resources. Do not expand S0 into runtime, transport,
ingestion, cancellation, feedback capability or general Memory refactoring.

## R1 — establish the complete Resources baseline

Map the real production tree before moving files. At review it consists of:

- `/resources`, `ResourcesPage.razor` and its code-behind: Registry, filters, typed editor,
  governed-storage summary, route resolution and Agent context;
- `ResourceFileBrowsePane`: source catalog, actual `FileBrowser`, promotion, read-only
  `FileInteraction`, downloads/preferred-app/open-folder actions and context publication;
- `ResourceStorageObjectPromotionDialog`: source/item selection, target project admission,
  name/sensitivity, owner result and notification to the parent;
- `ConnectorConfigFieldEditor`, currently in Workspace and consuming Security picker types;
  this is a real cross-module child dependency, not a harmless namespace import.

Inventory routes/queries, public consumers, dynamic children, parameters/events, DI,
scoped CSS, static web assets and any host-bound browser effect. Inspect current shared
FileTools/BaseLib contracts. Query Code Analytics and the Components MCP when available
and applicable. If unavailable, record that and use local source/call graphs/discovery;
do not invent tool output. WebGL is not part of this slice.

Capture the existing real desktop views, build/watch graph and edit loop **after S0 and
before Resources extraction**, using task-owned resources. Map existing tests by behavior,
not only names. Relevant starts are Resource portions of `OwnerPostcommitPageTests`,
`ResourcesPageAgentChatContextTests`, `ResourceFileBrowsePaneTests`,
`ResourceStorageObjectIntegrationTests`, and current Resource admission/projection/source/
connector/file-action/Memory snapshot consumers discovered in the checkout.

## R2 — create the real dependency cut

Suggested locations (reuse equivalents if already introduced):

```text
src/Modules/CanDoItAll.Modules.Resources.Contracts
src/UI/CanDoItAll.Resources.UI
src/Sandboxes/CanDoItAll.Resources.UiSandbox
```

A production module host composes existing owners with a typed workspace; the renderer
contains all meaningful Resources UI. The sandbox composes that same renderer and, where
shared, the same state policy with deterministic owners. Registry and Browse may have
separate cohesive sessions, but they must form one complete production workspace rather
than an extracted Registry plus an unchanged heavy Browse hidden behind a slot.

Move only stable value contracts out of mixed implementation files. Keep EF entities,
mappings, transaction coordination, connector executors/validation, source enumeration,
authorization, storage access and real launch services out of the UI dependency closure.
Preserve used namespaces, enum values, serialized shapes, defaults and assembly-resolution
requirements of actual consumers. Type forwarders are a compatibility tool, not a quota.
Do not move all of ResourceModels.cs or Workspace into Contracts to make compilation easy.

Reuse existing lightweight Projects contracts for `ProjectWriteAdmission` and choices.
Map secret picker data to the minimum reference metadata required; never load secret values.
Inspect the actual source-mode closure of FileBrowser/FileInteraction Core/Components and
integration abstractions before allowing them. A neutral `FileBrowserSession`, read-only
content port and appropriately scoped lease are legitimate UI inputs. A concrete Resources
coordinator, Infrastructure type or production registration hidden behind `object`, `Type`,
`IServiceProvider`, or an arbitrary callback factory is not a dependency cut.

Resolve the shared configuration-field edge deliberately. Prefer an existing suitable
shared renderer. A bounded extraction of the genuinely reusable field component/value
contracts may update its real Workspace callers with targeted compatibility proof, but
this does not authorize extracting Workspace as a module. A Resources-specific renderer
over projected schema is also possible if it owns real feature behavior; do not copy
shared component internals or create a parallel generic configuration engine. Preserve
validation, secret-reference semantics and raw incomplete numeric/JSON input.

Production Agent context/navigation remains host-owned; the sandbox uses a harmless typed
intent where necessary. Keep `/resources`, `resourceId`/`projectId` query meanings, canonical
navigation fence, registry/browse positions, readiness/failure and completion policy.
Do not grant capability based on presentation state or leak locator/secret/opaque authority
into serializable navigation.

## R3 — make Registry state and mutations correct

Retain the full existing search/project/connector/validation filters, list/detail behavior,
project/owner/maintainer/secret references, connector fields, metadata/sensitivity/capability
flags, location preview, Save/Reset/Delete and governed-storage read-only presentation.
Distinguish empty data, filtered-empty, pending, stale, missing exact resource/project and
unavailable references. Never interpret a missing existing resource as a fresh create.

Use stable draft and validation/EditContext ownership. Capture actual input before blur.
A tab change, list refresh, reference read or completed unrelated promotion must not discard
unfinished text, tags/configuration, validation, selected references or the editor context.
Do not reset every form through a broad `@key` whenever counts or snapshots change.

The current code already clones the submitted Resource editor; preserve and complete that
property. Capture the entire command, configuration and project admission before the first
await. Admit one conflicting mutation per editor/aggregate and allow genuinely independent
targets. Check direct handler dispatch, not just disabled buttons. Separate read generation,
editor/route target identity and mutation identity; a party lookup must not accidentally
retire a save result or authorize another write.

Accept the exact committed resource ID before secondary list/detail/reference reads.
Reconcile fields against the dispatched snapshot, with revision tracking when necessary to
preserve edit-away/edit-back. Preserve newer text and the same EditContext. An explicit
new project/draft has a distinct target lifetime; never attach the old result to it merely
because the visible public ID matches. No-op UI transitions should not invalidate work.

Treat known refusal, committed, committed-with-warning and unknown separately. Reuse
`ResourceCommittedMutationException`; retain target-labelled known warnings after the
operator moves away, as existing postcommit tests require, without mutating the successor
editor. An exception/cancel does not prove the metadata write rolled back. A retry of a
list/detail read is not a retry of Save/Delete. Unknown recovery requires explicit exact
identity and operation/request fencing; do not repeat the Scheduler review race.

Missing connector and schema changes must remain visible. The current page normalizes an
unresolved connector to the first manifest and filters configuration. Do not silently adopt
another plugin or discard stored configuration. Preserve deliberate owner migration rules;
explicit operator connector changes may apply a clearly defined migration/reset policy.
Keep unknown safe metadata unless the authoritative schema policy deliberately rejects it.
Preserve unavailable secret/party IDs with an honest label, not an invented selection.

Project lifetime/provenance is not optional: capture current selection explicitly, preserve
historical admissions on load, reject stale admission at the owner mutation boundary, and
keep global/resource-only historical reads distinct from current project-scoped reads.
A recreated project with the same public ID is not the loaded lifetime. Deletion keeps the
existing exact cleanup policy after retirement. Do not add a schema migration, optimistic
concurrency protocol or permission framework for this extraction.

Location preview should be cheap and side-effect-free. Do not perform DB/network/secret
reads per render. Keep owner connector semantics authoritative without cloning a registry
of executable plugins into the renderer.

## R4 — Browse, promotion and effect ownership

Ship the actual FileBrowser and read-only FileInteraction. Preserve all four source classes,
source health/capability display, paging/search/activation and governed stable-object view.
The source key is identity, not authorization. Owners re-resolve source configuration,
revision, actor access and target project admission for every relevant action.

Retain current limits unless an intentional change is separately justified: source catalog
maximum 512; page size 50; search at most 32 containers, 2,000 items, five seconds, one
concurrent request, 200 matches and 2 MiB retained search data; preview maximum 16 MiB;
file-browser session retention disabled. Keep bounded shared streaming, not whole-file
buffers or unlimited recursive enumeration. Do not broaden browsing permissions.

Give catalog load, selected-source open, file activation/preview, promotion and file actions
independent origin/request ownership. Refreshing a catalog may not restore the source
captured before an await over a newer selection. Preserve the existing stale-open disposal
behavior, including A-B-A, but also cover resources acquired after the host was retired.
Detach or capture a lease before async disposal; a late cleanup must not clear/dispose a
successor's preview or browser. Dispose newly acquired stale handles exactly once.

A promotion draft captures source key/scope/revision, exact item key, target project
admission, name and sensitivity. A new dialog lifetime or source selection must not change
what an admitted command targets. Later success belongs to that original operation; it
must not reopen its old source, reset a successor dialog or overwrite unrelated draft text.
Capture result identifiers before awaiting the parent callback. Failure of a parent refresh
or callback is not a failed persistence operation, and logging must not dereference fields
that Reset has already cleared.

Preserve the real owner chain: current source resolution -> exact read-only item activation
-> current actor authorization -> stable occurrence validation -> project-lifetime-checked
metadata write. Persist the stable source/storage/provider/locator contract, never browser
access handles, bearer URLs or arbitrary absolute paths in place of that contract.
Existing deduplication is scoped to the exact project lifetime and stable config; do not
turn it into a global/name-based idempotency guarantee.

At the promotion owner, preserve a confirmed ResourceId/Created outcome when revision
publication, a subsequent read, logger or cleanup fails. A missing revision is unavailable,
not zero/current. The current writer/service loses a returned identity across some
post-write effects; add the smallest typed observation at the true owner boundary and
prove it with actual persistence. Do not wrap every exception as committed: uncertain
commit acknowledgement remains unknown. Preserve the primary failure and known fact when
a diagnostic/cleanup failure follows. This same rule applies to the existing Resource
Save/Delete committed-exception logging path. Cleanup failure is still reported and tested.

Reopen a persisted storage object through the existing owner so current source/actor and
file-access state are rechecked. A late preview may not mount under another source or
component instance. Keep View mode and disabled edit/mode switching. Local open, containing
folder and downloads keep their exact authorized item/scope/capability semantics and
browser/host distinction. Never convert a source label or raw locator into a privileged
launch. A queued browser effect may be suppressed after retirement; an already admitted
external effect cannot be claimed undone. Keep a truthful original receipt.

Preserve current activation behavior: an internally supported file stays in the governed
promotion/preview flow; an unsupported pointer-double-click may open the preferred app
only when current local-launch policy allows; keyboard invocation keeps its documented
promotion behavior. Do not broaden these actions just to make the sandbox interactive.

## R5 — faithful sandbox and layered proof

Provide one directly buildable Resources sandbox with the same complete renderer, real
shared descendants and deterministic fake owner storage. No production DB/DI, live FTP/IPFS,
provider credentials, actual local application launch or Agent runtime is needed to render
or exercise its scenarios. Use real lightweight FileBrowser/Interaction APIs backed by
bounded fixture providers/content, not a fake HTML file list or a stub preview child.

Cover loading/empty/filtered-empty/representative/large data; existing/new/governed resource;
missing and retired project/connector/party/secret references; normal/invalid raw fields;
all source classes; selected source/item, promotion and preview; partial/stale/failing reads;
accepted/held/refused/unknown/committed-warning writes; policy denied/unavailable actions;
reset, A-B-A and disposal. Fake writes update their original store exactly once so later
reads can verify the ID and contents. Scenario replacement retires only its own work.
Labels distinguish synthetic FTP/IPFS/local-launch examples from real transport proof.

Move scoped CSS with the actual DOM and inspect every descendant boundary. Include all
FileTools/BaseLib scripts, styles and fonts, any intentional host JS and published static
asset paths. Theme linking as content is allowed; a Web ProjectReference for styling is not.
Verify source and published standalone hosts without production configuration. Inspect
real supported large-desktop screenshots, focus/caret, scrolling, clipped controls and
dialog stacking. Do not redesign the UI, add mobile layouts or use screenshot-only proof.

Execute [the matrix](VALIDATION_MATRIX.md), not a fixed test count. Start with targeted
current discovery and failing-first regressions; build each changed production project.
Refresh each owning assembly before `--no-build --no-restore`. Explain theory expansion,
zero tests, skips, platform limitations and unavailable MCPs honestly. Do not silently
drop existing Resources or other-module cases because types moved.

For production proof use the real Web route, real owner/PG registry CRUD, actual governed
file promotion/reopen/read and current authorization. Only narrow fault/transport boundaries
may be substituted to control timing. Use harmless task-owned files/projects/storage and
verify cleanup/revocation. Never launch an operator's application or alter ordinary storage
for a test. Existing FileTools integration proofs and bounded effect doubles can establish
safe host-action intent; distinguish them from actual platform-launch proof.

Widen validation to actual affected Workspace field callers, Workbench projection consumers,
Resource Memory snapshot/admission and HTTP/serialization consumers when the dependency or
contract move reaches them. Preserve their owners; these are regressions, not additional
module extractions. Full Stable is required only by current named invalidation rules or a
requested broad checkpoint, not merely after every phase. Record the widening decision.
`portability-static` is mandatory regardless: full proposed-tree scan including new files,
review added/stale findings, repair real defects, inspect justified baseline deltas and
final enforcement without `--write-baseline`. Run current documentation/evidence and secret
gates. No broad baseline acceptance or test quarantine to conceal regressions.

Measure actual evaluated project/package/native/runtime closure and watch inputs in source
mode. Separately measure original Web, extracted Web and sandbox startup and supported
Razor/C#/CSS/JS edit-to-visible behavior using [DEV_LOOP](DEV_LOOP.md). The goal is a useful
lightweight sandbox; no guaranteed whole-Web speedup or absolute time target is asserted.

## Safety, completion and delivery

Keep ordinary app/database port 5032 and other processes untouched. Use isolated PostgreSQL
18 per current test instructions, task-owned ports/files/configuration and bounded cleanup.
Record actual sibling revisions; do not modify sibling repos without separate authorization.
If a true missing shared contract prevents completion, document the minimal required change
and continue independent authorized work; do not copy its implementation or claim closure.

You may make local signed commits. Preserve the existing Git/GPG setup and reuse the
unlocked session/agent where available; never disable signing, replace identity/keys or
export secrets. No push, merge, reset, deployment or ordinary application restart is
requested. Do not commit runtime logs, credentials, generated binaries or proof screenshots.

Update maintained Resources/Memory boundary docs and local READMEs with actual design,
behavior, limitations and evidence. The input bundle is a sealed handoff, not a second
canonical architecture tree. Report S0 and R1-R5 separately, source drift, changed projects,
compatibility decisions, exact test filters/discovery/executions, real versus synthetic
browser proof, measurements, static gates, known blockers, cleanup and signed commit SHA.
Do not equate the reviewer's package tests or the previous implementer's 260 selected
cases with newly executed product proof.
