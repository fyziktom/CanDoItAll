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
