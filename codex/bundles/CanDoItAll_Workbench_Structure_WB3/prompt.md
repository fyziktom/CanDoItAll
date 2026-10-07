# Execute Workbench Structure Authoring WB3

You are the senior C#/.NET and Blazor implementation engineer using Codex GPT-6
Astra. Complete this entire bounded assignment in validated stages. A longer run
is authorized for useful implementation and native testing, not repeated reports,
interface proliferation or broad tests after every small edit.

## Outcome

Preserve the completed WB2 Insights/Selection and WB1 Planning boundaries. First
repair the existing structural-dialog completion issue described below. Then
extract the real main Project Structure authoring surface and its structural
editing family, wire production to it, provide a backend-free representative
sandbox and validate the real graph owners and affected application journeys.
Do not stop after the initial fix, a toolbar, or the first dialog.

This is not an API-only conversion or a Workbench runtime rewrite. Existing
in-process services, HTTP/control-plane integrations, project lifetime admission,
canonical graph ownership and recovery keep their actual responsibilities.

## Entry, instructions and signing

Read current `AGENTS.md`, `.github/copilot-instructions.md`,
`docs/architecture/ui-component-seams.md`, `docs/testing.md`, the current CI
workflow and applicable local instructions. Use the repository-family shared
standards skill when available. Read this package's shared v3 foundation once;
its historical audits are not commands to redo completed slices. Current product
instructions prevail over historical sample paths and test counts.

Record actual main/Components/FileTools refs, dirty state, SDK, source mode and
relevant build inputs. Reviewed main is `bbd9e8de96dc7895abdecc04406766f7aea94c8e`;
reviewed Components is `24d182c664d0b1f293098643e52caed7384a5d50`. Do not reset or
switch to those commits. The Components Gantt fix is now remotely available;
do not reimplement it or ask for an already-completed push. Verify the version
actually used by builds and served assets. Review changes since this snapshot.

Arrange existing native PGP/pinentry unlock early when required. Retain the same
host user, GnuPG home, persistent shell and gpg-agent for later signed commits.
Never request a passphrase in chat, export keys, disable signing, or put credentials
in logs/containers. Make coherent signed commits after meaningful stages and
verify them. No push, merge, release or historical-bundle cleanup is authorized.

## S0 — focused continuity and hierarchy lifetime repair

Read [WB2_REVIEW.md](WB2_REVIEW.md) and
[S0_HIERARCHY_LIFETIME.md](S0_HIERARCHY_LIFETIME.md). Do not replay the entire WB2
campaign just to establish entry. Preserve its qualified broad result, current
native evidence and SDK CSS limitation.

The existing `ExecuteProjectHierarchyCommandAsync` dereferences the mutable
`projectHierarchyDialog` after awaiting its owner. Canceling it or opening B while
A waits can make A fault, update B's error, or close B and select B's target.
This is a source-derived finding in the old authoring path, not a claimed native
incident or a defect attributed to WB2. Prove it first using the actual dialog and
a deterministic owner boundary. Cover success, known rejection, unconfirmed
result, same-target reopen and two independent views. Audit and close the same
pattern in the in-scope block-conversion and subtree-transfer dialogs.

Capture the opening, immutable submission, original targets/admissions and receiver.
After a confirmed owner action, preserve its original facts independently from
whether its view remains current. Only the original live opening may receive
error, busy, focus, close or refresh updates. Enforce duplicate admission inside
the handler; disabled buttons are not the gate. Do not use global close-all,
unconditional resets or blind replay of an uncertain write.

If a newly reproduced issue requires a new durable protocol or schema, map its
actual boundary and isolate the affected action rather than inventing a broad
framework. Small demonstrated defects should be fixed in this run.

## W1 — establish one real Structure authoring boundary

Read [SCOPE_AND_ARCHITECTURE.md](SCOPE_AND_ARCHITECTURE.md) and
[NATIVE_OWNERS_AND_OUTCOMES.md](NATIVE_OWNERS_AND_OUTCOMES.md). Inventory the current
route, actual Razor children, dynamic/generic dialog activations, template and
custom setup registrations, `@ref` commands, asset ownership and test callers.

Prefer `src/UI/CanDoItAll.Workbench.Structure.UI` and
`src/Sandboxes/CanDoItAll.Workbench.Structure.UiSandbox`. Add a small contracts
assembly only for a demonstrated native/rendering boundary. Do not move the mixed
`ProjectWorkbenchModels.cs`, metadata serializers, storage configuration, native
runtime objects or all Workbench types wholesale into Contracts.

Use the existing real CanvasWorkbench, stage, generic composer, accessibility
mirror and overlays. The new leaf must own the actual canvas/toolbar/toolbox
composition, not accept the entire old canvas as a RenderFragment. It must not
instantiate the old routed page or depend on Workbench, Foundation, App composition,
MAF Core or Processes implementations. Keep completed leaves and their graphs
unchanged unless a small justified shared correction is separately proven.

Native hosts own canonical graph reads, admission, dynamic choices, semantic
catalogs, state persistence, writes, Agent context and deferred integrations.
Presentation-local element refs/focus/JS ownership may remain in the renderer.
Choose a cohesive view contract or explicit presentations per real lane; do not
create an interface or service per input field, or a service-provider bag.

## W2 — complete the canvas and generic authoring

Read [CANVAS_AND_COMPOSER.md](CANVAS_AND_COMPOSER.md). Complete the route shell's
loading/unavailable/error/retry presentation and supported Canvas/Gantt/Manager
Summary tabs, actual stage, toolbar, standard-block toolbox, search/grouping,
selection mirror, authoring modes, node movement, dependency connect/reconnect,
viewport/window state and generic node create/edit composition.

Keep the native structural catalog and its dynamic option loaders. Preserve
special task, secret, text-asset, image and owner-managed paths as explicit host
integrations; ordinary node editing must not bypass their native editors. Runtime
configuration may be edited as metadata without starting a process.

Every composer result must retain the original opening and project/node identity.
The old `TryApplyNodeEditAsync` looks up the target in the current surface: test
stale completion under route, lifetime, source-node and dialog replacement before
retaining that pattern. Carry existing admission to the actual writer; never
manufacture fresh authority from a public ID after a wait. Preserve richer
metadata, references, unknown extension fields, temporal precision, intentional
zero coordinates and all unedited data. Do not interpret a read failure as an
empty configuration or overwrite metadata wholesale from a partial form.

Prove same-node echoes, section switches and unrelated snapshot updates do not
silently discard the draft, raw invalid input or its validation context. A real
new target must retire it. Keep the current deliberate post-submit editing policy;
if editing continues, merge acknowledged values against the submitted draft and
preserve later changes. Do not introduce silent saves when switching views.

## W3 — complete structural operations and dialogs

Read [GRAPH_AND_HIERARCHY.md](GRAPH_AND_HIERARCHY.md). Finish the actual clipboard
copy/cut/paste, structural movement/reparenting/recomposition, exact deletion
confirmation and existing cleanup presentation, block reclassification/note
conversion, add/reconnect-subproject and move-descendants-to-new-subproject dialogs.

Retain canonical task restrictions, system-managed/read-only nodes, cycle checks,
exact link kinds/IDs, branch-root normalization, source/target project lifetime,
original creation receipts and transfer/cleanup dispositions. Copy and Cut have
different native semantics. Do not add cross-project paste or new Duplicate behavior
where the current product refuses it. A new project created for transfer is not
permission to retry creation after a lost reply.

Opening the existing Projects editor from hierarchy must remain functional, with
exact return identity and draft ownership. Do not extract Projects again. Fix
small per-opening/state/outcome mistakes at their real boundary; retain known
native stages before any reload. Reconciliation reads must not re-run a graph
mutation or external action.

## W4 — integrate without widening the cut

Use [DEFERRED_INTEGRATIONS.md](DEFERRED_INTEGRATIONS.md). Mount the existing WB1
planning and WB2 report/support leaves through production composition and narrow
active slots. Preserve their origin-bound intents and admitted writes. The
Structure shell cannot turn them into children of a new giant shared state bag.

Keep party/meeting/assignment, secret creation, transcript/image generation,
file browsing/content viewers, terminal/web preview, Workflow/Process linkage and
launch/recovery with their current production owners. They must still open and
return to the correct original target. Mark these renderer families as deferred;
a working slot does not mean they were extracted. Do not start their wholesale
extraction or the Processes product module.

## W5 — prove the completed family on final sources

Run [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md) and
[APPLICATION_JOURNEYS.md](APPLICATION_JOURNEYS.md). Build every changed production
project first. Confirm exact test discovery and refresh owning assemblies before
`--no-build`. Use current repo helpers for renderer-dispatched event lookup/click
and asynchronous component disposal. Preserve failed attempts and their effects.

Prove the real canvas gestures and accepted native graph, not only text in the DOM.
Check exact identities, links, metadata and untouched neighbors after writes.
Prove the source and independently published sandbox use actual components without
backend registration; test two independent surfaces, nested dialogs and errors.

Use an isolated native project created through Projects UI. Exercise graph and
hierarchy edits, WB1/WB2 consumers, an Agent tool approval and refusal, hidden file
canary read, create/attach/readback/download, and relevant Workflow/Scheduler/History
continuity. External model replies may be deterministic; owners, native tool and
approval paths, storage and persisted evidence must be real.

Use the owned source/two-client fixture for affected shared-provider and native
consumer paths on the final application image. Retain model display-name versus
opaque route-ID semantics and prevent fallback. Rerun all 19 protocol vectors
when the actual change invalidates them; do not call an inherited earlier result
fresh execution. No paid inference or reset of an exhausted live budget.

Use only large desktop, primarily 1920x1080/DPR1. Do not spend this run tuning
mobile/tablet breakpoints. Measure three reversible samples each for relevant
Razor, C#, scoped CSS and feature JS (only if owned/changed). Record PID, automatic
or manual navigation, restart and served/computed result separately. Preserve the
known native SDK CSS hot-reload qualification; do not label restart as hot reload.

One deliberate final Stable checkpoint is warranted if the cut changes native
writers/public contracts/shared components or test/CI composition. Apply the exact
current named trigger rules, not broad runs after every dialog. Freeze and hash
source and test DLLs before running. A mixed full run remains mixed after focused
repairs. Run mandatory portability enforcement and required scoped secret reviews;
do not delete historical controls or narrow scanners to claim a clean checkout.

## Delivery

Provide the implemented scope and remaining Workbench map, real dependency/asset
census, exact refs and verified signed checkpoints, commands/discovery/results,
failed-attempt dispositions, final-image applicability and developer-loop samples.
Keep private payloads, credentials, TRX, screenshots and logs in owned ignored
artifacts; maintained docs contain safe summaries and exact references.

Finish the complete WB3 family. Do not claim all Workbench or the entire application
release-ready. Do not start Processes or an unbounded platform-wide redesign.
