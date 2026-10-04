# Workflow authoring WF1

Execution of `codex/bundles/CanDoItAll_Workflow_Authoring_WF1/prompt.md` on the
existing `components-decoupling` branch. The sealed package and historical bundles
are unchanged. The full selected rendering family and native Workflow consumers
are implemented and proved, including final History. Frozen Stable accounting is
still running; final signed closure is pending. Earlier stage notes below retain the
state and limitations of their original checkpoints.

## Entry and progression

| Stage | State | Evidence / remaining closure |
|---|---|---|
| S0 | CA1-R1 complete; signed `ccd18f6a1` | Original 6/12 failures retained; repaired 28/28, later capability assembly 68/68 and native host follow-up 4/4 |
| W1 | Lossless definition and accepted identity complete; signed `4df9f9339`, extended in `ac0e2e231` | Six real native round-trip cases plus controlled save/conflict/retirement cases |
| W2 | Complete canvas, windows, inspectors and actual settings; signed `ac0e2e231` | Independent 30/30; real gestures, exact Gallery version and saved coordinate proof |
| W3 | Complete template, preview-input, run/event dialogs and native composition | Native 146/146; late image read 4/4; library acquisition repair `950704afc` passes affected 95/95 |
| W4 | Source/publish, measured edit loop, final-image Workflow consumers and History proved | The single broad Stable checkpoint remains in progress; final gates/signing follow |

Entry source pair: main `28ca64aaf0a2e3f65ee44feae2d931852bd18611`, Components
`b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`; all three worktrees were clean.
Main adds only the sealed WF1 package over reviewed `ccca2fd3a`. SDK is 10.0.303;
isolated build configuration is `WF1`, with evaluated sibling-source resolution.
CI separately pins FileTools `498b36825bd5a5222429972af120b04becf4b3f6`.

Configured RSA signing identity `96E836FAA8854EE98ABC10903C206549E1D7EAD6`
successfully signed and verified a harmless probe through the existing GPG agent.
The local Components Tooltip commit verifies with that identity. A read-only remote
query still reports development `4a858412d2c2a3f6123bf23d8c4584f05b47627d`.
The fix is local-only; no remote delivery is claimed. The freshly built capability
sandbox's BaseLib assembly matches the sibling output byte-for-byte, SHA-256
`4e6717da01f99be459f414a65567b2e0e3777699061038fdeaf4269472f85acb`.
W4 confirms the loaded source, published and native Canvas asset bytes. Components
also contains the signed read-only drag repair `2eccdddd05a9b1b0c90935ddee49dc559fd5eec1`;
both fixes remain local-only, with remote development unchanged.

CodeAnalytics, Components and dotnetwatch MCP methods are unavailable in this
session. The authorized fallback is current source search, evaluated MSBuild
references, CLI builds/watch and real Playwright. No unavailable MCP is reported as
executed. SharedInfo tooling and Git standards were read; repository testing, CI
and UI seams remain authoritative. The execution, component-composition and C#
architecture skill gates apply to subsequent extraction stages.

## S0: setup evidence attribution

The real `CapabilityAuthoringForm` reproduced stale diagnostics after a successful
attempt followed by an unknown acknowledgement. The first selection discovered and
executed 12 cases: 6 failed, 6 passed, 0 skipped. A second unchanged-production
attempt put the actual rendered diagnostic assertion first and again recorded
6 failures / 6 passes. Both original attempts are retained privately.

`CapabilityAuthoringSession.TestSetupAsync` now clears diagnostic/tool evidence when
the current attempt becomes unknown. It retains the unknown-effect guard, draft and
accepted catalog facts. The notification directs the operator to inspect the native
result rather than suggesting a blind retry. No runtime setup owner changed.

The regression drives the actual form for Tool and MCP: same revision, A to B,
A to B to A, real unsuccessful results, edits during an outstanding acknowledgement,
late success/error after close or owner retirement, and independent editors. It checks
session facts and rendered text, no setup on Save, and no duplicate setup admission.
The controlled test boundary simulates acknowledgements; it does not replay unknown
native setup effects or claim new live-provider proof.

Affected production build: `dotnet build
src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI -c WF1 /m:1` passed.
The owning test project is
`tests/Components/CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests`.
Build-backed discovery confirmed the expected 28 cases for
`FullyQualifiedName~CapabilitySetupAttributionTests|FullyQualifiedName~CapabilityAuthoringStateTests|FullyQualifiedName~CapabilityAuthoringFormTests`.
Execution with the identical filter, `-c WF1 --no-build --no-restore /m:1`, passed
28/28, with no failures or skips. This includes 14 new cases and 14 retained
state/form cases. Existing AngleSharp NU1902 warnings remain qualified.

Portability tooling self-tests passed 6 and 4 cases. The fresh tracked-source scan
covered 8,387 files and 34,085 findings; enforcement passed with 15,205 reviewed
executable-source findings unchanged, without baseline-write mode. No protected
production file was added in S0. Final WF1 source requires a new full scan.

Private artifacts are under `artifacts/workflow-authoring-wf1`: original and final
TRX/logs, signing probe, evaluated references and the complete scan. Historical CA1
mixed Stable results and subsequent focused dispositions retain their original scope;
the S0 checks do not reinterpret them as a clean broad run.

## Initial boundary and validation decisions

The primary authoring surface remains the actual CanvasWorkbench with its three
floating windows and nested inspectors. Supporting information stays in its existing
tabs/dialogs, with compact counts. Canvas owns its viewport; wide/full dialogs retain
their own scrolling body and visible action footer. All new browser checks use
1920 by 1080 at scale 1.

The existing `Workflows.UI` shell retains its ASP.NET/BaseLib/Charts-only graph.
A separate `WorkflowAuthoring.UI` owns actual rendering, local graph editing and
lossless presentation state; native module adapters retain catalog, Prompt,
component, trust, profile and execution authority. Its independent sandbox must
compose the same children without production DI. Adding the native canvas graph to
the old shell or merely moving injections into another partial does not satisfy this
boundary. W1 must settle the exact dependency closure before the extraction.

Focused selection is the stage default. One final frozen broad Stable checkpoint
will be selected if new public authoring contracts, composition or test-solution/CI
registration changes invalidate the prior aggregate. No broad S0 run is justified.
Native PostgreSQL, source/two-client images, file approvals, Scheduler, held responses,
accepted/incomplete LLM output and History journeys remain pending. Existing retained
containers and ordinary port 5032 are outside this task's fixture ownership.

### Responsibility decisions

This is a project-boundary extraction with native adapters, using the architecture
governor, modular-refactoring and project-boundary-extraction skills. Before-state:
the module's `WorkflowCanvasEditor` mixes local editing, native effects, metadata
reads and 1,500 lines of actual markup; `WorkflowsPage` also renders its overlays.
The implementation is staged so each accepted checkpoint remains runnable.

| Responsibility | Target owner | Independent proof / principal risk |
|---|---|---|
| Lossless whole-document baseline and edited node/edge projection | Authoring UI document model | Native exact-version title-edit comparison; hidden fields and null semantics |
| Canvas, toolbox, floating windows, inspector and nested node/route forms | Authoring UI components with local typed edit state | Isolated real-component sandbox; occurrence identity and raw edits |
| Save admission and accepted identity reconciliation | Authoring session with a narrow native save callback | Held completion, accepted-before-refresh and foreign-conflict tests |
| Prompt compatibility and component creation | Native binding adapter; leaf renders Prompt UI through an explicit selection seam | Exact prompt/version/provider pair, committed component survives callback failure |
| Preview authority, admission and progress | Native preview adapter; leaf submits immutable graph/input and receives attributed results | Retired owner prevents new dispatch, progress belongs to its originating occurrence |
| Schema settings, image settings and secret metadata | Actual authoring/settings renderers; native trusted host selects allowed implementations | Schema/raw JSON retention, denied trust cannot acquire a renderer |
| Template/preview/run/event dialogs | Authoring overlay renderers; page retains authorized reads and writes | Real native callbacks plus isolated open/close/late-read cases |
| Built-in route syntax/value rules | Cohesive neutral Workflow definitions code shared by runtime and authoring | Runtime route tests and direct editor tests; no runtime implementation dependency |

The old module canvas becomes a native composition host. The extracted components
must render independently of that host; neither service-location nor another native
partial is the boundary. A single broad service bag was rejected because it would
leave effect authority and lifetimes inside the renderer. New settings implementations
use the existing trusted registry rather than adding executor-ID branches.

W1's first two native reproductions failed (2/2, no skips): a valid title save drops
all input descriptors, and a rich graph with nullable shapes is rejected after mapper
reconstruction. After retaining the baseline, both reach acceptance and graph/input
comparisons pass; a subsequent full-record assertion exposed PostgreSQL timestamp
precision: immutable definition JSON retains 100 ns ticks, while the owner uses the
microsecond-precision creation column when creating the next version. The fixture
reads the original exact stored version and allows only that specific creation-time
truncation, name, new version and update time. Original attempts remain in private
artifacts; the first expanded selection was 27 passed / 2 timestamp assertions failed,
0 skipped. The amended two-case native round-trip selection passed 2/2, 0 skipped,
with freshly built binaries. The other 27 cases passed in the expanded attempt;
that original 27/2 result is retained. The affected module direct build passed.
This proves the title-edit baseline slice, not the full round-trip matrix, held-save
reconciliation or completed authoring family. The fresh portability scan passed
with 15,205 reviewed executable-source findings unchanged and no baseline write.

## W2/W3 implementation checkpoint — in progress

The actual canvas and all three floating windows now live in
`CanDoItAll.AgentFramework.WorkflowAuthoring.UI`. Shared route fields, execution
policy, exact provider/model selection and the full eight-field image form are
real child renderers. Both preview-input paths, template catalogue/read-only canvas,
run detail and event detail are extracted. The native page is composition, and
ordinary shell tab changes retain its mounted editor. Native adapters retain
catalog writes, exact Prompt compatibility/component creation, trusted settings
resolution and preview authority/admission. No Workbench or Processes extraction
has started.

Evaluated WF1 sibling-source graphs: old shell 4 projects (same paths as entry),
authoring UI 17, new sandbox 19, prior catalog sandbox 16. None of those evaluated
closures imports the AgentFramework implementation module, Web, Workflow Core or
persistence. Pure routing validation and execution-policy limits now belong to the
neutral Workflows.Definitions project; native runtime/compiler behavior delegates
to those rules. The native module retains runtime evaluation and effects.

The new independent component assembly passed its initial 19 cases, then 21 after
failing-first validation/preparation read errors. The original two failures are
retained. A later three-case Prompt lifetime selection reproduced one stale
new-node callback crossing into a replacement document (2 pass / 1 fail). The
callback now captures its originating document, while accepted components remain
available after callback failure or retirement. Expanded final focused results
will be recorded before checkpoint signing.

Native composition passed 67/67 with no skips. Expanded native round-trip and
ownership selection discovered 54 cases (6 round-trip; 48 ownership). Its original
53/1 result is retained: the one failure was the new node-edit test's tab locator,
corrected to click the actual tab button. Authority capture canceled by target
retirement dispatched zero runs; known acceptance and unknown reserved identities
survived their respective lifetimes in both preview entry paths. Native follow-up
and the remaining final campaign are still required.

Source desktop inspection at 1920×1080, scale 1 exposed missing Canvas assets and
an incomplete image descriptor fixture. Both were repaired using the actual neutral
asset components and a native descriptor snapshot. The current source sandbox
renders the actual canvas, two independent editors, Gallery picker and full image
settings without browser console errors. Original failed captures/logs remain
private. Published, Fast/Parity and repeated edit measurements are pending.

One deliberate final Stable run is required by the public boundary, native preview
admission handling and new test/CI wiring. No broad result is claimed yet. Required
native Web, deterministic source/two-client images, file approvals, human response,
Scheduler, History and accepted/incomplete output journeys remain open. Private
logs/TRX/fixture credentials stay under the task-owned artifact roots; only reviewed
metadata is exported here.

### Rendering/composition checkpoint results

The final expanded independent selection passes 30/30, zero skipped. Rich-port
projection, port-schema preservation during a node value-shape edit and chosen
connection endpoints first failed 3/3; the repaired native projection preserves
names, IDs and required flags. Insertion and single-path reconnection carry the
unchanged outer port endpoints. Invalid fan-out text remains visible and is refused
when applying a route. The three Prompt lifetime cases now pass, including the
original stale-callback reproduction and accepted-before-refresh retention.

Fresh native discovery and execution passed 146/146, zero skipped: WorkflowsPage
44, ownership 48, round-trip 6, executor catalog 12, executor display 2, image
settings 3, provider/model selector 11, Gallery picker 6 and lightweight Workflow
surfaces 14. A subsequent image-read lifetime test first reproduced disposal of a
still-pending read token (0/1); a linked read lease repaired it, and the complete
four-case image renderer class then passed 4/4. These overlapping selections are
reported separately. All 15 changed production project owners built directly;
authoring UI, native module and sandbox were rebuilt for their final source deltas.

Portability self-tests passed (6 baseline; 4 scanner). The complete tracked scan
contains 8,442 scanned files and 34,108 findings, with no truncation. All 46 added
and 45 stale baseline entries were reviewed: moved presentation code, retained
native secret-reference metadata reads, case policy, canvas link fields and the
sandbox README's shell fence. Source routes use ordinal identity; display search
and GUID/schema protocol matching retain their intended comparison rules. The
reviewed baseline moves from 15,205 to 15,206 allowances; final enforcement passes
without baseline-write mode. Documentation validation passes for 356 maintained
Markdown files.

The full modified-file text export's secret scan retains 20 findings in the existing
CI fixture configuration. A separately scanned HEAD copy matches every rule,
fingerprint and multiplicity; none is introduced by the three new project-list
entries. Original failed scans are retained, not suppressed. Changed-line and safe
metadata export results are recorded separately from this qualified whole-file
result; no whole-worktree secret-scan PASS is claimed.

The isolated source/two-client preflight passed for the fresh
`shared-providers-e2e-pp2-wf1-9188e9a3` fixture (loopback ports 62540–62542;
PostgreSQL 62545, disk-backed PostgreSQL 18). Preflight changed no containers or
credentials. The historical stacks remain untouched; the final image campaign has
not yet run at this checkpoint.

## W4 results and retained attempts

The protocol checkpoint uses the owned
`shared-providers-e2e-pp2-wf1-9188e9a3-r2` fixture on loopback ports 62640–62642,
with disk-backed PostgreSQL 18 on 62645. Source inputs are main
`ac0e2e2319572f464d9b19e2ef7442c11954e294`, Components
`2eccdddd05a9b1b0c90935ddee49dc559fd5eec1` and the entry FileTools revision.
The initial native Workflow authoring browser test was untracked at image build.
The exact image input fingerprint is
`2341c4ead70a577b4f83391e24f43017847846cc8880802caa551eeee012e432`.
The application image ID is
`sha256:cfdebbf133af60e5fbc5d494cbca8d581439ba0f80e0292a34dfd92a05c36925`;
the deterministic upstream image ID is
`sha256:379e2164a53bada49ba3cc38c95d8ad8325e8d20447211c33aea9e4a56bbe3ce`.

The protocol checkpoint passes 19/19. Chat streaming first data arrived at
853.863 ms and completed at 1081.0631 ms (227.2001 ms incremental interval);
Responses first data arrived at 430.0121 ms and completed at 972.9213 ms
(542.9092 ms interval). Both exceed the unchanged minimum 50 ms incremental
interval. Original attempts remain: missing explicit reset authorization for an
otherwise new fixture, then an overlapping Docker network. The successful fresh
fixture uses independently verified unused networks; no historical resource was
reset or removed. The final source-default/custom-model publication, import and
refresh journey passes 1/1 across source and both clients.

The first stable-image native consumer selection passes 5/7: Simple Chat default
and nondefault transcripts, real PNG generation/attachment/download/vision,
incomplete Workflow output refusal, held human continuation and actual Quartz
delivery pass. The original failures are retained. The accepted-output helper
expected an unadorned default model label; the real selector displays
`Provider default (source name)`. The file helper clicked before waiting for the
interactive Projects owner and applied search. Its earlier native mutations,
hidden-canary read, approval, denial and preview had already succeeded. A read-only
continuation downloaded the same stored file with matching bytes and hash, without
replaying those mutations. Focused corrections and the graph/Gallery journey are
recorded separately from that mixed attempt.

### Native library acquisition repair and replacement image

The corrected native consumer selection passed the entire file/approval/download
journey and both accepted/incomplete Workflow/TestLab cases (3 passed), while its
new graph/Gallery case failed before adding the first node. A readiness correction
waits for the new canvas dependency identity. The next graph attempt exposed a
real page-owner race: New draft canceled an in-flight component-library read, then
failed to start a read for its new owner. Three controlled component cases first
failed for late success, failure and cancellation. `StartCanvasDraftAsync` now
awaits the existing library acquisition after retiring the previous selection.
All 95 freshly discovered WorkflowsPage/WorkflowOwnership cases pass, zero skipped.
This repair is signed and verified as `950704afc6e387f7844e3492fd2084ef0a12aa7d`.

The new application image was built from the prior commit plus the exact repair,
with input fingerprint
`031cdb6a632b6a3cf34764d9c37cbd7104ed255cf71880ca54eca064e4bc4716`
and image ID
`sha256:d250d0ef00f9e5a4b6acabb737e592ff26f2173edc8c0d7b45bb1002535de91f`.
Its revision label remains the actual build base `ac0e2e2`; it is not relabeled
with a later commit. Only central/client-a/client-b were replaced. Their mounts,
the database/upstream container IDs, credentials and native receipts are retained.
The resolved Compose comparison changes only the application image. The original
19-case protocol metadata is retained separately; affected Workflow and History
journeys run on the replacement image without a second protocol vector.

The first replacement startup exposed an adjacent Scheduler runtime defect:
reprojecting an enabled fixed-date CRON whose sole occurrence already completed
throws Quartz's "will never fire" exception and stops host startup. The retained
original consumer receipt identifies plan `dd42dedf-6b26-4eb7-90c0-f86808776560`,
Workflow `a3726147-5b0c-41c2-9c8f-9589d27a57d5`, immutable version
`ef7e9c9d-3889-40b2-9bdf-a4bfd7e22c59`, and completed run
`b7451640-899a-49bf-893e-df4e62c0b02b`. With the host unavailable, task-owned
fixture cleanup used one guarded SQL update matching that exact plan, name,
target/version, CRON and completed/no-next-fire state to pause it; only one row
changed. No definition, run, receipt, file or historical fixture was deleted.
The same client container then started healthy. Subsequent Scheduler proof pauses
its completed plan through the native UI. This cleanup does not repair Scheduler:
a separate runtime change should classify exhausted schedules before trigger
projection, retain their terminal history and prove restart plus future/misfire
behavior. No Scheduler production source was changed in WF1.

The repaired image's graph journey passed provider loading, exact Gallery binding,
node creation/deletion and route editing before a test-only Playwright result
conversion failed (a positional record has no parameterless constructor).
Coordinate observations now use JSON deserialization into that typed record;
the original attempt remains retained.

### Long route labels and final application image

Native desktop inspection reproduced route rows expanding to 1,981 pixels inside
a 357-pixel inspector when a node name contains 200 unbroken characters. The
renderer now permits wrapping and gives the text column a shrinkable basis.
Source, independently published Parity and the native final image each measure
357-pixel client/scroll width, with 326-pixel rows and reachable Edit controls at
1920×1080, scale 1. The seven-line CSS repair is signed and verified as
`a60d4cb76c678713ec344609edbe41aa2e71cd41`.

The final application input fingerprint is
`21a8d30b6cb73c073b028803ffa31cf3bbc833d2978e1295ff3896aae8a30895`,
with image ID
`sha256:85459b7a67e1324bd16cae58f4b8b0b9b5c1eac093d32327c680d875dcf90610`.
Its revision label is the actual production checkpoint `a60d4cb76`. The same
three application roles were replaced with this image; the database, upstreams
and mounts were again unchanged. The earlier library/protocol input receipts are
preserved. Subsequent changes are consumer-test observation/cleanup and evidence.

The graph test's retained pointer observations distinguish UI actionability from
native persistence: a success notification initially intercepted the node drag.
The test waits for the node point to belong to the canvas. On the successful
gesture, the scene moves by (70, 35); the transient manual-position map can already
be empty because the native node-moved callback has adopted the coordinates.
The final proof therefore compares actual saved versions before/after dragging,
and separately compares the graph after a fresh native read. It does not require
an internal transient map to remain populated.

The final-image six-case attempt retains five passing consumers and one failing
graph test. Accepted and incomplete Workflow/TestLab output, exact Agent/Project
file approvals and download, held human continuation and actual Quartz delivery
all pass. The graph test had already saved three versions and completed its native
run; its final assertion incorrectly expected payload text inside the metadata
API response. The actual authorized run-detail dialog displayed the output.
After changing that assertion to the real dialog and checking the API's exact run
identity, fresh discovery and execution pass the one graph case in 30 seconds.
The original 5/1 attempt remains a failed attempt; it is not relabeled 6/6.

The passing graph journey proves native UI node creation/deletion, protected Start,
route label edit/removal and physical port reconnection; the next Save preserves
the workflow identity, creates a new version and persists both moved coordinates.
A fresh native read compares the entire graph. Gallery binding creates one native
component with the exact original Prompt/version/model route. After advancing the
Gallery head, a saved Workflow run dispatches once with the original instructions,
never the successor text, and renders the accepted original output. The retained
component still names the original immutable version. These checks use the final
`85459b7` image, not the earlier protocol image.

The final three History/circuit/source cases pass 3/3, zero skipped, in 5 min 10 s.
Global and provider History return matching native attempt identities with lazy
authorized content reads; limited credentials are denied and rotation retains the
exact caller identity. Two circuits preserve concurrent local settings until an
explicit merge of only the edited field. A disabled source returns 503 without an
upstream request; retirement and reimport preserve the local provider identity.
An invalid HTTP/private-network policy edit is refused and leaves its original
source state intact. Browser error/request-failure arrays are empty.

A final separately discovered native navigation case passes 1/1 in 43 seconds.
It keeps an unsaved name across Dashboard, Workflows, History and Analytics, then
changes to a different Workflow. Browser Back and Forward restore each exact saved
definition; the old unsaved draft does not cross the target change. Native version
IDs remain unchanged throughout navigation. This closes actual browser-history
proof in addition to the controlled route/profile/Curator owner tests. The same
final image was restarted for this bounded check and stopped again afterward;
no preview, scheduled delivery or provider inference was dispatched by this check.

Final source/client-a/client-b container logs were retained and classified. Expected
incomplete Responses, History access denials and canceled validation of the exact
rotated credential remain visible. Existing development-vault, EF mapping/query
and retryable History maintenance cancellation warnings remain qualified. None of
the three logs contains an unhandled exception, rendering error or disposed-token
marker. This is a bounded campaign review, not a claim that all logs are clean.

The six verified final fixture containers and three partial first-attempt containers
are stopped; their volumes, images, credentials and original receipts are retained.
Source/Parity/Fast watcher processes and the task browser are stopped. The separate
isolated PostgreSQL test container remains running only for the frozen Stable lane.

### Independent desktop and development loop

Both independently published Fast and Parity hosts use the actual canvas assets,
four canvas layers and 1920×1080 at scale 1. The Parity source/publish checks cover
the 82-node graph, 1,200-character input, raw invalid JSON refusal, unavailable
dependencies, held/unknown preview, owner replacement, Gallery presentation, all
eight image fields, template/catalogue and run/event dialogs. Two editors retain
different dependency IDs; disposing one leaves the other able to save. IF,
SWITCH/default and fan-out inspectors render actual route fields. Invalid fan-out
index text remains visible and cannot commit. Modal footers and radial menu bounds
remain inside the large desktop; focus returns to CanvasWorkbench. Physical route
handles reconnect an edge. Window drag changes its position by (120, 30), and
minimize/expand restores its size. Middle-button pan changes the viewport by
(80, 50); zoom and focus-start operate on the actual canvas.

Physical template dragging exposed an existing shared CanvasLib defect: read-only
nodes were still admitted into drag operations. Four failing-first JavaScript
cases cover direct, mixed-selection, frame and dependency drag. The shared guard
now excludes missing/read-only nodes. The signed Components commit above passes
`assets:verify`; source and independent publish retain six template nodes, empty
manual positions and identical coordinates after physical drag and Delete.
The served JavaScript is 64,632 bytes with SHA-256
`71cc087022f1619a990bb4856c74df4e9a92fb40f2a9d2eebfce59ae0c04741d`.
Both Components fixes remain local-only; no remote delivery is implied.

| Visible edit | Three edit-to-observation samples | Activation / restart |
|---|---|---|
| Razor heading | 3,999 / 2,405 / 1,822 ms | Same rendered component, PID 17072 |
| C# presentation constructor | 1,443 / 1,325 / 1,161 ms | New component activation, same PID |
| Scoped CSS border | 8,530 / 7,815 / 7,902 ms | Actual computed border, same PID |
| Owned JavaScript zoom | 2,272 / 1,684 / 1,660 ms | Browser reload, same PID; rendered node width 369.6 / 403.2 / 436.8 px |

All four probe files match their original SHA-256 hashes byte-for-byte. No warm
sample restarted the application. The fresh-process cold attempt built in 28.12 s
and loaded 19 watch projects in 6.5 s. Its 30-second HTTP probe expired during
startup; the eventual verified browser observation was 66.862 s after dispatch,
including orchestration gaps, with new PID 60132 and six visible nodes. This is an
upper-bound cold observation, not a hot-reload timing. Earlier refused-connection
and observation-script failures remain retained. Watch paths reduce from 4,681
for Web to 768 for the sandbox on SDK 10.0.303. Evaluated final graph counts are
shell 4, authoring 17, sandbox 19, prior sandbox 16 and native module 135 (entry 133).
The shell's exact project path set is unchanged.

### Frozen Stable accounting

The public contracts, native preview boundary and new test/CI selection justified
one deliberate final Stable checkpoint. The solution does not define configuration
WF1, so that original rejected solution command is retained. Each of its exact
27 project entries was then built, discovered and executed once with the current
Stable exclusion filter against the isolated disk-backed PostgreSQL 18 fixture.
The campaign is not a clean broad PASS.

Unit results are 9,430 executed, 9,425 passed, 5 failed, zero skipped. Discovery
lists 9,396 cases: three nonserializable theories expand from 1 to 8, 1 to 8 and
1 to 21 at execution, accounting for all 34 additional cases. Four failures are
stale expected dependency/presentation ownership assertions; their corrected
36-case focused selection passes. The fifth detects preserved synthetic historical
scanner artifacts from A2/CA1. Those originals were not deleted or hidden.

Native component results are 2,503 executed, 2,501 passed, 2 failed, zero skipped.
The two failures expected the old setup-error notification in the surviving
capability details/wizard hosts. The corrected four-case selection passes and
still verifies unknown acknowledgement, no extra setup admission, private-error
redaction and disposed-token lifetime. Within the original frozen component run,
all 147 selected Workflow/Prompt/provider/image cases pass; this is an extracted
result set from that broad attempt, not another execution or a claim that the
entire component assembly passed.

### Final renderer and caller census

| Surface | Reusable rendering | Native owner retained |
|---|---|---|
| Canvas and all three floating windows | `WorkflowCanvasSurface`, neutral CanvasWorkbench/OverlayToolbox/Canvas floating windows | Routed `WorkflowsPage` and small `WorkflowCanvasEditor` composition |
| Definition, node, edge and decision editing | Canvas surface, `WorkflowRouteFields`, `WorkflowExecutionPolicyEditor` | Native definition/version semantics and runtime/compiler validation |
| Provider/model and immutable Prompt binding | `WorkflowProviderModelSelector`, actual Prompts.UI picker through its typed slot | `WorkflowPromptBindingOwner`, Prompt Gallery and component library |
| Generic and image executor configuration | Actual configuration composition and `WorkflowImageGenerationSettingsSurface` | Vetted renderer key, owner/trust/schema checks, provider reads and secret-reference metadata |
| Both preview-input paths and result presentation | `WorkflowPreviewInputDialog`, shared existing result surface | `WorkflowPreviewOwner`, immutable submission, native authority, admission and recovery identity |
| Template catalogue and read-only canvas | `WorkflowTemplateCatalogDialog`, `WorkflowTemplatePreviewDialog` | Template pack reads and native Add-to-drafts receipt/provenance |
| Run and event details | `WorkflowRunDetailDialog`, `WorkflowEventDetailDialog` | Lazy owner reads, bounded safe formatting, original run/attempt and canonical History identity |
| Five-tab shell, catalog, History, overview and analytics | Existing Workflows.UI surfaces retained | Query validation, Curator/context readiness, database profile and route lifetime |

No page-owned presentation in the selected Workflow family is deferred to another
slice. Native hosts still own reads, writes, authority and accepted receipts; those
integrations are intentional remaining callers. Residual global Agent chat/usage,
Voice and provider/runtime dialogs remain in their existing owners. Workbench and
Processes have not been started. The standalone authoring sandbox has no production
module, database, provider runtime or Web assembly edge.
