# Workbench content and files WB4

WB4 implementation and native journey proof are complete. Final broad Stable disposition,
documentation closure and the last signed handoff are still being recorded. This is a bounded
Content extraction; the whole Workbench is not complete.

The sealed [WB4 package](../../codex/bundles/CanDoItAll_Workbench_Content_WB4/prompt.md)
is unchanged. Its G00–G35 contract governs closure. Private source fingerprints, binaries,
commands, discovery, original failures, native receipts and screenshots are retained under
`artifacts/workbench-content-wb4`; the evidence checker validates structure, not authenticity.

## Source pair and delivery

Entry application `1687d701e4a8ed4ed63261bfaffbfc12cb6a619f` differed from reviewed WB3
`3957fe73e2a0736c042daa504a2767050423c36b` only by sealed inputs. The required Components
`a120106bc3d4576a40c16aac29b1b9654fb31d93` was present, signature verified and fetched from
remote development; its tree equalled WB3's tested isolated dependency. It was not reimplemented.
FileTools `3a080ecd31068a77c1e1bd639f7a78e21c93db85` remains unchanged and tree-equivalent
to CI's `498b36825bd5a5222429972af120b04becf4b3f6`.

WB4 requires the new signed Components checkpoint `a3fd4d22f2c4e0432cf389f194c6468b44ab7371`.
It extracts the existing neutral canvas overlay into CanvasLib while preserving Structure's
public compatibility wrapper. Its 31 Canvas tests and asset verification pass. Reviewed
approval changes include six previously missing WB3 asset fingerprints without modifying
those assets. This new dependency is **verified locally only**. No push, release, package
publication or reproducible remote CI readiness is claimed.

The final native application image was built from `6fd90de11e886b5d4480d83243d86d64b09ca1c1`
with that exact Components/FileTools pair. W4 changes after this revision are test/fixture
and documentation changes. The owned source and both clients use the same application image.

## Implemented boundary and current callers

The [UI seam policy](ui-component-seams.md) determines placement. The new
`src/UI/CanDoItAll.Workbench.Content.UI` owns actual rendering, presentation state,
children, scoped CSS and asset registration. `src/Sandboxes/CanDoItAll.Workbench.Content.UiSandbox`
provides independent scenarios. The module retains original native authority and effects.

| Family | Actual rendering owner | Retained native owner and integration |
|---|---|---|
| Text, JSON, Markdown, Mermaid and log creation/upload | ContentTextForm, bounded upload reader and draft session; actual FileUpload | Original text dialog/coordinator maps immutable submissions to native normalization, original project admission, parent occurrence and asset creation |
| Project and node file collections | ContentFileCollectionWindow with the actual FileBrowser and FileInteraction | Native scope resolver, collection coordinator, grants, source revision, leases and local actions |
| Direct known-file editing | ContentFileInteractionDialog and native library editors | Original preview adapter and governed save target, current capability/revision checks, session release and close guard |
| Image setup and phase feedback | ContentImageDialog | Original page admission, provider catalog, bounded queue, worker, media storage and native binding |
| Progress Summary and status controls | ContentProgressSummaryDialog | Original accepted row/root snapshot, status mutation, XLSX/Mermaid/canvas-image asset writers |
| Transcript scaffold and analysis | ContentTranscriptDialog | Original recording/transcript links, explicit provider invocation and serializable metadata/reference update |
| Legacy Mermaid and Markdown children | ContentLegacyMermaidDialog, WorkbenchMermaidFileView and WorkbenchMarkdownMermaidBlock | Strict eligibility and original editing authority; managed content stays on FileInteraction |

The current census covers 403 rendering/activation/asset files. Evaluated MSBuild graphs
cover 28 roots: the 25 preserved leaf closures are unchanged, Content resolves 12 projects,
and its independent sandbox 14. Workbench adds Content UI only. No reverse dependency from
Planning, Insights, Structure, Agents, Workflow or Workspace was introduced. The Content
closure contains no native module, EF, database, provider implementation or Processes owner.

The ten actual FileInteraction children are exercised through native library composition,
not replacement markup. Neutral CanvasOverlayDialog is shared with Structure; the original
Structure wrapper preserves compatibility. Original Mermaid fully-qualified names resolve
through assembly type forwarding. Existing native media/status method signatures remain.

The adapters project typed state and intents; they do not render the former module inside
a RenderFragment. Page partials retain route-owned authority/lifetime orchestration as the
canonical seam policy permits. There is no service bag, new filesystem abstraction, replay
platform, schema migration or duplicated native serializer.

## Original targets, revisions and outcomes

WB4-T1 was reproduced before extraction through the actual text dialog and writer: a held
InputFile submission wrote a native node and 64-byte asset into a replacement lifetime with
the same public project/root IDs. Capturing the original project admission and parent/node
occurrence at opening fixes the original boundary. Immutable fields, upload identity,
actor/profile generation and the original receiver remain attached across awaits.

Retired A→B→A openings and late success/error/finally callbacks cannot obtain new authority,
clear a successor's busy state or release its handles. Owned sessions detach before async
cleanup and release once. Known accepted identities survive failed observation. An unknown
post-dispatch acknowledgement requires observation and cannot enable automatic resubmission.

Collections activate read-only interactions. A direct known file may edit only through its
native save target. Current storage write/mutable-update capability is checked when issuing
and resolving a grant. This repairs the reproduced pre-existing permission-loss defect where
an earlier grant stayed writable after the storage became read-only. Revision conflicts,
dirty/pending/conflict close guards, exact bytes and supplemental Notes remain distinct.

Image placeholder commit, enqueue acknowledgement, provider completion, media storage,
node binding and view reconciliation remain separate facts. The original 64-item channel is
volatile, not durable replay storage. Requests retain original authority/placeholder identity
through worker scope; current provider purpose/configuration/model membership is checked.
Duplicate delivery cannot repeat a completed operation. Saved media with failed binding keeps
its receipt; interrupted or uncertain work is observed rather than regenerated.

Progress Summary exports create stored graph assets from their accepted rows/root/date
snapshot. Inline status reconciles that same snapshot. Transcript scaffold creates a typed
recording reference and DerivedFrom link without speech recognition. Each analysis requires
explicit provider confirmation, persists only its output field and creates no tasks. Unknown
metadata and original references survive. Provider completion is distinct from successful
native persistence. Legacy Mermaid is eligible only without managed attachment authority;
missing storage access never licenses a Notes fallback.

## Executed proof

Counts below describe distinct selections, not an additive unique test total.

| Selection | Verified result |
|---|---|
| S0 original/native text and WB3 composer lifetimes | 34/34 |
| W1 independent text leaf | 19/19 |
| W2 final independent content leaf | 26/26 |
| W2 native collection/editor component boundaries | 31/31 |
| W2 file/normalization/authorization units | 111/111 |
| W2 real PostgreSQL editor/revision/permission integration | 7/7 |
| W2 original local-dispatch authority | 2/2 |
| W3 final independent content leaf | 40/40 |
| W3 native action lifetimes and generated image boundaries | 53/53 |
| W4 public compatibility and original resource picker | 5/5 |
| W4 native download and harmless local process journey | 1/1 |

Native host proof acquires two independent leases for exact bytes, rejects a released lease,
launches one reviewed harmless probe through the original preferred-application owner and
then denies capability-loss/headless actions without another process or canary change.
Its explicit host fixture is documented in the Integration README.

Four independent source/published Fast/Parity hosts render all five Content routes at
1920×1080/DPR1. All 20 screenshots were physically inspected. Ten child renderer types,
two independent instances, upload/keyboard use, Save/conflict, strict Mermaid, 128-row Summary
scrolling and image choices pass. Seventy served scripts/styles/fonts per host agree across
identity/gzip decoding: 560 asset requests. Three apply/restore probes each for Razor, C# and
scoped CSS retain one process and browser time origin; all 1,963 copied source files were
restored byte-exact. No owned JavaScript changed. This measures the loop, not a universal speed.

The final native source/two-client protocol passed 19/19 scenarios. Separate real application
journeys cover operator-created projects, all five text kinds, invalid JSON rejection,
BOM/CRLF upload, exact revision-aware Save, parent/node/descendant collections, read-only
activation, frozen Summary status and decoded stored XLSX/Mermaid/canvas PNG. Existing accepted
files were observed and continued after automation failures rather than recreated.

Default/alternate model choices preserve friendly labels and opaque routing IDs. Six final
native image receipts across two clients match three independently decoded PNG/JPEG/WebP
fixtures, native operation/parent identities and exact hashes. The deterministic upstream's
old JPEG and WebP literals were malformed; only those test bytes were repaired. The prior
accepted malformed output remains, and its viewer correctly shows a fallback. Two owned
external fixture containers were replaced after archiving captures and confirming no active
plan; all application/database containers and the application image remained unchanged.

The three transcript analyses pass with exactly three external fixture calls, no call on
scaffold/cancel, exact stored output fields, original recording/provider references, unknown
metadata and zero tasks. Their existing History owner remains Direct/Standalone with caller
unavailable and uncaptured content; WB4 does not invent a transcript History owner.

Actual Agent read/write/attach/download and exact approval/refusal receipts pass. WB1 task,
Calendar/Gantt and assignment/pricing values, WB2 Summary/Activity/descendant/currency behavior,
WB3 native canvas/selection and Workflow/Scheduler consumers retain their owners. Scheduler
survives two restarts and executes its future occurrence once. Human response, SimpleChat,
standard runtime/stop, canonical History, source policy and two-circuit provider settings pass.

Usage initially reports the incomplete Agent index honestly. The existing explicit maintenance
tool completed only the owned test organization's derived index: all 349 canonical JSON
payloads remained byte-identical. Three Usage dialogs and independent 7d/14d queries then
agree with 19 canonical Agent observations plus two real PostgreSQL SimpleChat invocations:
21 observations, 378 fixture tokens. Client A identities do not appear in client B or source.
No paid inference was used.

## Original failures and their disposition

Original failed/aborted runs remain failed in the private ledger.

| Original observation | Disposition |
|---|---|
| Held text upload targeted replacement lifetime | Original creator/admission capture repaired; same regression and expanded native suite pass |
| Issued edit grant survived storage write-capability loss | Native authorization rechecks current capabilities; two-editor and revoked-capability integration passes |
| Unstable child callbacks caused render loop | Stable composition callbacks; final leaf selection passes |
| Busy button from retired dialog leaked into successor | Opening-keyed subtree; late success/error tests and two-instance browser proof pass |
| Resource-picker assertion used removed Structure CSS class | Same stretch/unbounded assertion targets neutral CanvasOverlay class; 5/5 focused compatibility cases pass |
| Floating final reply missed the 60-second test deadline | Native completion two seconds later; same run/session/intent reopened, rejected proposal and absent denied file verified; no repeated approval or provider request |
| Content browser stopped behind overlapping saved windows | First accepted file preserved; native UI continued remaining operations; helper hides unrelated windows using real controls |
| Image test read placeholder before completion | Native completed-state oracle, preserved original operation and exact PNG readback |
| JPEG/WebP upstream literals were not decodable | Test-only fixture repair; all three formats decode through both native clients |
| Host-launch negative used refreshed bootstrap capabilities, then wrong collection source | Original effects retained; dedicated native storage routing and separate download asset prove the corrected journey |
| Ancillary browser/oracle assumptions about collapsed groups, singleton sources, test IDs, export formatting and pending rendering | Readback before continuation; no accepted action repeated; corrected observers verify actual native controls/bytes |

## C# architecture gate

Status: Pass for the implemented boundary. Final broad disposition remains separate.

| Check | Evidence and decision |
|---|---|
| Responsibility and construction | Rendering/drafts in the leaf; project/store/provider policy in original owners. No duplicate renderers, locator service or new composition container |
| Dependency direction | 28 evaluated graphs and current source/assets/caller census; 25 preserved leaf closures unchanged |
| Partial-class policy | Existing routed page owns native effects and lifetime under the UI seam policy. Extracted renderer tests do not instantiate it |
| Testability | Independent leaf/sandbox plus native PostgreSQL barriers, exact byte/revision/capability negatives and real browser consumers |
| Extension and compatibility | Production hosts use the new typed renderer seams; public forwards/signatures and asset activation verified |

## Validation and remaining scope

Final no-write portability enforcement passes with 15,280 reviewed executable-source findings
unchanged. Both required scanner-tool suites pass. Complete proposed diffs and all changed/new
UTF-8 sources are reviewed as text; raw secret scanning retains 26 findings, all unchanged CI
test/generated-value patterns or unchanged diff context. No introduced secret is found. This
is not a claim that historical bundles or retained private runtime artifacts are globally clean.

One broad Stable run was selected because native authority, queue, shared overlay and CI
membership changed. Its frozen production/test inputs are retained; test-only follow-ups use
separate configurations. The original run is not relabelled by a passing focused continuation.
Its final counts/disposition and documentation gate results will be added before closure.

Remaining Workbench integration work includes participant/meeting/directory assignment,
protected secret forms, terminal/runtime/web-preview and Workflow/Process linkage/start/recovery.
They remain reachable through their original native owners. They are not extracted by WB4;
Processes remains last. Existing WB1 Planning, WB2 Insights, WB3 Structure, Agents, Voice,
SimpleChats, providers, Projects and Workflow boundaries remain intact.

## Signed checkpoints

All checkpoints use the existing PGP key
`96E836FAA8854EE98ABC10903C206549E1D7EAD6`; signatures were verified.

| Repository | Commit | Checkpoint |
|---|---|---|
| CanDoItAll | `a19133d808ed0f6fef11ff92a48a5549a7689de5` | Original text target repair |
| CanDoItAll | `3851f5eee513f5fb1c6d66ed29d24635aa41c002` | Complete text creation/upload |
| Components | `a3fd4d22f2c4e0432cf389f194c6468b44ab7371` | Shared neutral overlay |
| CanDoItAll | `c4c88afad5ff70449167ed690c189f554b875c29` | Collections and governed editing |
| CanDoItAll | `6fd90de11e886b5d4480d83243d86d64b09ca1c1` | Image generation, transcript analysis and stored exports |
