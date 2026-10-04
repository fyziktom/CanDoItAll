# Workflow authoring WF1

Execution of `codex/bundles/CanDoItAll_Workflow_Authoring_WF1/prompt.md` on the
existing `components-decoupling` branch. The sealed package and historical bundles
are unchanged. This is an in-progress implementation record, not family closure.

## Entry and progression

| Stage | State | Next concrete work |
|---|---|---|
| S0 | CA1-R1 reproduced, repaired and signed as `ccd18f6a1`; focused checks pass | Final source campaign |
| W1 | Initial lossless repair signed as `4df9f9339`; full matrix under validation | Exact native unchanged/node-edit/conflict and acceptance checks |
| W2 | Canvas, windows and actual inspector/settings children extracted | Final native/desktop consumer proof |
| W3 | Template, preview-input, run/event dialogs composed through shared renderers | Expanded native ownership checks and checkpoint gates |
| W4 | Pending | Native consumers, source/publish desktop, final images and closure gates |

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
Final loaded-browser asset verification remains part of W4.

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

## Planned boundary and validation

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
