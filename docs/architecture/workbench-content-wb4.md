# Workbench content and files WB4

Status: implementation in progress. This record does not claim WB4 or the whole Workbench complete.

The immutable input is [WB4](../../codex/bundles/CanDoItAll_Workbench_Content_WB4/prompt.md).
Its 36 evidence groups remain the acceptance contract. Private attempts, original failures,
hashes and screenshots are retained under `artifacts/workbench-content-wb4`.

## Entry and preservation

Entry application: `1687d701e4a8ed4ed63261bfaffbfc12cb6a619f` on `components-decoupling`.
The changes since WB3's `3957fe73e2a0736c042daa504a2767050423c36b` add only sealed input packages.
Components: `a120106bc3d4576a40c16aac29b1b9654fb31d93`, signature verified and fetched from
remote `development`. Its tree equals the independently tested WB3 dependency. This is
not a claim that remote `main` contains it. FileTools: `3a080ecd31068a77c1e1bd639f7a78e21c93db85`;
its tree equals CI's `498b36825bd5a5222429972af120b04becf4b3f6`. All three checkouts were clean.
Package integrity and 42 package / 14 shared tooling tests pass; the 22 shared files match WB3.
These checks certify input structure only.

## Architecture decision before implementation

| Responsibility | Current owner | Intended owner and proof seam |
|---|---|---|
| Text form, raw draft and bounded browser upload | Workbench text dialog | Content UI renderer and typed submission port; isolated renderer tests |
| Project admission, original parent/node, storage create and exact outcome | Workbench page and text coordinator | Original native owners; snapshot-bound creator, native PostgreSQL regression |
| Collection window and its real FileBrowser child | Workbench file browser window | Content UI; host supplies scoped native collection reads and activation |
| Direct editable file interaction and close guard | Workbench preview dialog with FileInteraction | Content UI presentation; native governed session, revision checks and release remain native |
| Generated-image setup and progress | Workbench support dialogs/page | Content UI; original queue/provider/media owners retain captured origin and distinct phase receipts |
| Progress Summary and exports | Workbench support dialogs/page | Content UI rows/actions; native accepted-source status and stored-asset exports |
| Transcript confirmation and action presentation | Workbench support dialogs/page | Content UI; native content revision, provider invocation and metadata commit remain separate |

Use `CanDoItAll.Workbench.Content.UI` as a cohesive rendering leaf and
`CanDoItAll.Workbench.Content.UiSandbox` as its independent host. The leaf must own actual
markup and child renderer composition, not forward an old renderer fragment. It may depend
on neutral Components/FileTools mechanisms and narrow stable contracts. It must not depend
on Workbench, EF, Infrastructure, provider/runtime implementations or Processes. Existing
Planning, Insights, Structure, Agents, Workflow and Workspace leaves must not acquire a
reverse dependency. Preserve public namespaces and compatibility forwards where needed.

The selected adapter is a typed owner port/captured delegate: it separates native authority
from rendering without duplicating policy. Merely moving files into another page partial
was rejected because it cannot provide independent rendering or protect delayed callbacks.
Existing page code-behind may retain route-owned lifetime orchestration under the canonical
[UI seam policy](ui-component-seams.md); this is not the extraction boundary. No generic
operation journal, schema migration, service locator or fallback authorization is planned.

## Ordered execution and proof

1. S0: reproduce WB4-T1 through the original rendered text dialog and actual writer, with a
   held upload across same-public-ID project recreation. Capture original source/binaries.
   Repair the native opening/creator boundary before extracting it. Prove accepted outcomes,
   stale callbacks, exact stored bytes and untouched neighbors.
2. W1: complete all five text kinds and both create/upload modes, immutable submissions,
   bounded validation and phase-aware recovery.
3. W2: full collections and governed direct editing, exact session/handle cleanup, concurrent
   revision conflicts, read-only collection activation and actual permitted host commands.
4. W3: image presentation and original queue origin, stored Summary exports, transcript
   confirmation/concurrency and both legitimate Mermaid paths.
5. W4: complete caller census, evaluated dependency graph, source/published Fast and Parity
   sandboxes, native operator/Agent/Workflow/Scheduler/source-and-two-client consumers,
   measured repeated watch edits, static/documentation closure and signed checkpoints.

S0 selects `ProjectStructureTextAssetLifetimeTests` in `CanDoItAll.Tests.Components`,
with current build-backed discovery before execution. Adjacent text/coordinator/composer
topics are selected only as their contracts change. Source hashes, dependency mode, configuration,
test inputs and native fixture ownership invalidate reuse. Broad Stable is deferred to one
final frozen checkpoint: new shared public UI contracts and host composition are its named
invalidation trigger. Original broad failures must remain visible; focused follow-ups do
not relabel their aggregate result.

The independent sandbox must exercise actual child renderers without application bootstrap,
including two instances, delayed/failing operations and retired openings. Browser validation
uses only 1920x1080/DPR1. Shared controls and current semantic tokens remain authoritative.
Text editing is the primary dialog surface; metadata is supporting content. Keep useful
content and actions in the first viewport with one clear dialog/window scroll owner. Inspect
normal and open-overlay screenshots, keyboard behavior and geometry, not just DOM assertions.

Components, CodeAnalytics and dotnetwatch MCP tools are unavailable in this session.
Fallback evidence uses exact source/caller tracing, evaluated MSBuild graphs, builds and
real Playwright/browser/host observations. Paid model calls remain zero. Participant/meeting,
protected-secret, runtime/terminal/web-preview and launch/recovery integrations remain with
their existing owners; Processes is excluded.

## Current gate

Entry prerequisite: pass for bounded S0 reproduction. WB3 implementation and dependency
trees are preserved; its existing broad/watch qualifications are not erased. Native WB4
renderer extraction and final consumers have not yet been proven.

## S0 original-owner repair

The original failing run discovered and executed one test. Its held browser upload resumed
after the project and root were recreated under the same public IDs and the replacement
surface was loaded. The original delegate created a real file node and a 64-byte storage
asset under the replacement admission. `s0-original.trx`, the original build/discovery logs,
and `s0-original-source.json` / `s0-original-binaries.json` retain that failure and provenance.

Text creation now captures the original surface, admission, actor, navigation revision,
source/parent occurrence and opening. The bound creator supplies native expected participants
and admission; the UI does not mint new authority on resume. All draft fields, source mode,
upload descriptor, cancellation token, persistence callback and dialog receiver are frozen
before preparation awaits. The native receipt is recorded before rendering/follow-up.
Unconfirmed writes require observation and cannot be resubmitted from that opening.
An old dialog closes only its captured reference.

The expanded eight-case attempt passed seven and exposed one further defect: the native
create participant query omitted `ParentNodeKey`. Its original failure remains in
`s0-expanded.trx`. The create owner now uses the existing
`ProjectStructureNodeExpectations.ReadAndEnsureCurrentAsync` inside its serializable mutation
scope, preserving record, kind and parent checks without a second policy implementation.

The final S0 selection contains 34 cases: ten new native lifetime cases, the existing text
dialog/coordinator cases, and WB3 composer lifetime coverage. Discovery and execution both
report 34; all pass with zero skips (`s0-native-closure.trx`). It proves same-ID replacement,
changed kind/parent/record, actor change and A→B→A retirement, two independent pages,
immutable metadata, native accepted/lost acknowledgements after retirement, exact accepted
node/storage identity and SHA-256 byte readback. The known-created/follow-up-failed control
uses the real writer, then explicitly fails observation in the coordinator's creator port;
it is not a claim that a normal text create currently schedules a placement move.

Commands use `CanDoItAll.Tests.Components.csproj`, configuration `WB4`, source dependency
mode, and the filter `FullyQualifiedName~ProjectStructureTextAssetLifetimeTests|FullyQualifiedName~ProjectStructureTextAssetCreateDialogTests|FullyQualifiedName~ProjectStructureTextAssetCreationCoordinatorTests|FullyQualifiedName~ProjectStructureComposerLifetimeTests`.
The native harness uses a newly owned PostgreSQL 18.6 container on a dynamically allocated
loopback port and a named volume. It does not use the application Compose stack or any WB3
fixture. Credentials are private. Final no-write portability enforcement passes with
15,246 reviewed executable-source findings unchanged; no baseline rewrite was needed.

This checkpoint closes the reproduced original-target defect. The broader WB4 requirements,
including profile changes, browser upload limits, pending-error replacement, independent
source/published hosts and final native consumers, remain owned by W1–W4.

## W1 renderer seam decision

The existing public text dialog will remain a thin native adapter for compatibility. Its
form, raw draft, bounded browser reading, busy/error state and real FileUpload child move
to Content UI. The leaf receives a typed immutable text submission and a typed outcome port;
it does not reference Workbench's media service or native node/admission types. The adapter
maps to the existing content normalization/preparation service and snapshot-bound creator.
This avoids moving native authoring policy into UI or introducing a new contract assembly
only to share five text-form choices. Isolated renderer tests will exercise the new leaf
without native DI; the existing native harness remains the composition proof.

## W1 text extraction checkpoint

Content UI now owns the complete text form and actual FileUpload child. The native dialog
maps its immutable submission to the original normalization and persistence owners. The
typed outcome distinguishes preparation, committed identity, known partial commit, rejection
and unconfirmed dispatch. Cancellation after dispatch also requires observation. Uploads
are bounded while reading and must exactly match their declared length; the leaf preserves
the original bytes and leaves format/UTF-8 policy with the native owner.

The current native selection passes 34/34, and the independent leaf passes 19/19 with zero
skips. The source Parity sandbox ran two specimens at 1920×1080/DPR1. An actual BOM-prefixed
18-byte upload retained SHA-256 `4607B9844B4D6B1A32B4697A80604671B4CE469DABDF482FF6770A946F468708`;
the second specimen remained untouched. Browser automation setup errors are retained; the
successful readback observed the accepted attempt instead of resubmitting it. Final full
family source/publish/browser proof remains W4 work.

The new leaf, independent test project and sandbox are registered in the root solution;
the test project is in both Components/Stable solutions and all three explicit CI component
lists. Portability review identified four existing sandbox diagnostics patterns in the new
watch endpoint and the moved text's accepted-extension string; the corresponding old
text-dialog fingerprint is stale. These are intentional relocation/diagnostic findings,
not OS assumptions or production authority changes.

## W2 shared overlay decision

The existing preview must maximize inside its canvas. A first extraction using BaseLib's
document modal failed that continuity check. Content therefore needs the neutral overlay
shell already used by Structure. Moving that small shell to CanvasLib lets both leaves
compose it without a Content-to-Structure feature dependency. Structure keeps its public
compatibility wrapper; the shared shell owns the one rendering implementation and styles.
This measured need is the bounded sibling edit permitted by WB4's signing/delivery rules.
The original WB3 dependency remains verified; the new sibling checkpoint will have its own
source-pair proof and delivery status. No remote delivery is inferred from a local commit.

The resulting Components checkpoint is `a3fd4d22f2c4e0432cf389f194c6468b44ab7371`, verified
with the existing PGP key. It is local only. Its 31 Canvas tests and asset verification pass.
Approval deltas include the new overlay and the retained WB3 composer callbacks, opening
types and six asset fingerprints missing from earlier snapshots; those WB3 sources were
unchanged. This checkpoint does not establish reproducible remote CI/package readiness.

## W2 collection and direct interaction

Content now renders the real compact FileBrowser, collection previews and direct editor.
The native adapters retain scope resolution, project admission, original node occurrence,
file grants, save targets, local commands and downloads. Retired results, errors and cleanup
are fenced by opening and operation. Cleanup detaches its own handles before awaiting;
held predecessors cannot release successors or acquire a new lease through a dead callback.
Direct interactions keep accepted revisions across mode changes, reject Diff, and retain
dirty/conflict/pending close guards. Supplemental notes remain separate from file content.

A native counterexample found a pre-existing FileTools authorization defect: a previously
issued edit grant remained writable after its storage became read-only. The existing
authorization coordinator now checks current write/mutable-update capability on grant and
resolve. The original 5/6 run remains failed. The repaired independent-storage selection
passes 7/7, including two live editors, current revision conflicts, revoked handles and
permission loss. An intermediate capability test incorrectly used bootstrap storage whose
capability mask is deliberately refreshed; its failed 6/7 result is retained separately.

The initial renderer tests also found unstable child callbacks causing a render loop; the
aborted run is retained, and stable callbacks pass all 26 leaf cases. Native component checks
pass 31/31 after adding held activation success/error and exact release ownership. Focused
file/normalization/authorization unit checks pass 111/111; the added local-dispatch origin
checks are recorded separately. None of these overlapping selections is a unique suite total.

Source Parity browser checks at 1920×1080/DPR1 render ten real content children and two
independent windows. An accepted text save was observed through View mode with SHA-256
`659F53A43D485D3EF52823626D82A913F8929141CFF19D4632563A7A6656B67F` and a count of one;
an initially stale scenario receipt caused a selector timeout, not another Save. The receipt
now updates on completion. The old 440×560 collection default left its toolbar obscuring
rows; a 600×700 default with a bounded minimum height makes pointer navigation usable.
PNG bytes decoded, XLSX cells/formula rendered, Mermaid used no HTML labels, and the neighbor
stayed unchanged. Actual native launch/download and final consumer proof remain W4 work.

Portability delta review covers FileTools namespace/type/reference matches moved into Content
and its scenarios, plus original native action code. These are existing governed library
calls or neutral contracts, not new shell or operating-system dependencies. Removed native
fingerprints are reconciled in the same checkpoint.

## W3 generation, analysis and stored exports

Content owns the complete Progress Summary, transcript confirmation, generated-image setup
and legitimate legacy Mermaid presentation. The native support component is now a typed
projection adapter. The deferred delete dialog remains with its WB3 owner. Source model
labels are separate from opaque routing values; a source-managed image profile cannot accept
a custom model. The dropdown's HTML uses option indices, while its typed submission retains
the original model ID. Both choices are tested through the actual control.

The page captures original admission, node occurrence, actor/profile generation, opening,
summary rows and transcript content before asynchronous work. Native content mutations check
that expected metadata, notes, storage reference and typed references still match within the
existing serializable mutation. Unknown JSON properties survive transcript and image metadata
updates. Summary exports retain their accepted rows and root. Canvas capture retains the
original canvas instance; a valid accepted action may finish at its original target after
navigation. This does not authorize a recreated target or a different actor/database generation.

Image generation retains the existing 64-item single-reader in-memory channel. The request
carries its accepted placeholder occurrence and original native authority into the worker's
scope. Provider purpose, enabled state, configuration and source model catalog are checked
before dispatch and attachment. The worker changes the original queued metadata before
external dispatch; duplicate delivery cannot repeat a completed request. Restart is not a
durable replay guarantee: interrupted native state stays observable without automatic resend.

Placeholder creation, queue acknowledgement, provider invocation/completion, stored media,
node commit and view reconciliation are distinct facts. Native media replacement preserves
a typed storage receipt if bytes were saved but binding commit failed or its acknowledgement
was lost. No failure handler overwrites a possibly committed image with a generic failed state.
Public request/result identities and the original metadata-factory overload are preserved;
additional receipt fields are additive. Native deferred requests lacking original authority
are explicitly refused. The current caller census finds only the bound page producer.

Transcript scaffold creates an empty native transcript and recording reference/DerivedFrom
link; it does not invoke speech recognition. The three explicit analysis commands capture
content and provider once and save text in their respective metadata fields without creating
tasks. A concurrent content revision or post-provider native save failure leaves one provider
attempt and an observation-required result. Known created nodes survive link/readback failure.
Legacy Mermaid remains eligible only without managed attachment content, renders strict/no
HTML labels, and its Edit/Close callbacks retain the original opening. Stored .mmd never falls
back to Notes when a content grant is unavailable.

The native image selection passed 17/17 before the final enqueue-refusal case was added. It
covers original project/placeholder replacement, actor revocation, changed content/provider,
worker interruption, duplicate delivery and both pre-commit refusal and post-commit lost
acknowledgement after media storage. Content/Insights native checks passed 32/32; three new
legacy tests initially used a canvas command that is not their real caller, and their failed
attempt is retained while they are corrected to the actual Selection Panel entry. Independent
leaf tests pass 40/40 after repairing one test's mistaken raw-option-value assumption.

The two-instance browser campaign first exposed missing sandbox placement parameters, which
caused its summaries to overlap. Native document-scoped placement is preserved; the scenario
host now explicitly requests canvas-scoped dialogs. The first accepted scenario exports and
status change were observed as three actions, with zero in the neighbor; their initially stale
receipt was a missing scenario-owner render, not a reason to repeat the actions. These failed
and continued observations remain separate. Full final native and publish/watch proof is W4.
# W3 checkpoint validation

The final focused leaf run passed all 40 discovered cases. The native PostgreSQL run
passed all 53 discovered action-lifetime and generated-image cases. The earlier corrected
legacy caller plus image boundary run passed 21 cases. Original failed attempts remain in
the private evidence ledger, including an additional browser-reproduced lifetime defect:
the shared asynchronous button retained its busy state after its dialog opening was replaced.
Opening keys now isolate the Summary, transcript, legacy Mermaid and image dialog subtrees.
Both success and failure completions are tested before the predecessor is released; the
large-desktop browser verified the new image form stays enabled and keeps its draft while
the old result publishes no receipt. Its independent neighbor recorded zero submissions.

Source screenshots were inspected at 1920×1080/DPR1. The image form exposes friendly
model names and dispatches the opaque alternate ID. Empty, loading, partial and unconfirmed
states refuse submission. Summary buttons, transcript disclosure and strict legacy SVG
remain visible; browser readback confirms the Summary Close control lies inside its dialog.
The regenerated Fast and application styles were reviewed. Portability enforcement passed
without write mode at 15,280 reviewed allowances (one MIME protocol comparison added and six
obsolete image option comparisons removed); documentation validation passed for 376 files.
This checkpoint does not close the final application, published-host or multi-client campaign.
