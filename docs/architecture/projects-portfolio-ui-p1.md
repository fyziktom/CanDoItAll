# Projects portfolio UI P1

Historical P1 checkpoint: **Projects P1 complete / Files P2 deferred at that checkpoint**.
The subsequent [Projects Files P2 record](projects-files-ui-decoupling.md) records the
two bounded P1 follow-ups and the separate Files extraction. The original S0 remains qualified as
`REVALIDATED_WITHOUT_ROOT_CAUSE`; application release readiness is not certified.

## S0 entry — 2026-10-01

Disposition: **REVALIDATED_WITHOUT_ROOT_CAUSE**. The exact
`SharedProviderTwoInstanceUiAcceptanceTests.Provider_empty_client_imports_shared_providers_and_runs_chat_image_and_vision`
case passed from empty, newly owned publisher/client databases, through real import,
metadata mirroring/resynchronization, authentication and scripted external responses.
Discovery: 1; executed: 1; passed: 1; failed/skipped: 0. The original OpenAI-then-Ollama
order and option assertions were unchanged. No paid inference was authorized or used.

Six new controlled component cases plus 23 existing session/surface cases passed (29
discovered/executed, no failures/skips). They cover late provider acquisition, acknowledged
selection, metadata after a local selection, A→B→A, same-source echoes, retired views and
independent activation, preserving exact provider/model identities and typed edits.
Production Simple Chat code and assertion deadlines were unchanged.

Safe DOM timelines show each newly acquired dialog initially rendering no provider options,
then the provider list, then a dispatched selection followed by its three model options.
The Ollama acknowledgement arrived about 1.15 seconds after dispatch. These traces observe
DOM identities/options, not server read generations or a causal explanation of the R2
failure. Deterministic session tests separately exercise those generation fences. The
historical one-option anomaly was not reproduced and is not claimed repaired.

This residual is isolated to the observed Simple Chat selector presentation. The current
exact path and negative controls show no wrong-provider authority or lost durable state;
Projects uses its own draft, owner admission and reconciliation. This supports qualified
P1 entry without declaring application-wide readiness.

The tested application source starts at `038a2696f31291cfda5b34b88db75f28405472cd`
with only the S0 test instrumentation/controls; Components is
`4a858412d2c2a3f6123bf23d8c4584f05b47627d` (tree
`1e318a37f187c120e74d88357715ba22ae5cec31`), FileTools is
`3a080ecd31068a77c1e1bd639f7a78e21c93db85` (tree
`6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d`). SDK 10.0.303, source references,
`ProjectsUiProof`, PostgreSQL 18.6, viewport 1920×1080. The Linux source/client publish
used the same source pair. Evidence is retained under
`artifacts/projects-ui-p1/20261001-038a2696`: `s0-components*`, `s0-cold-01*`,
`publish.log`, `owned-fixtures.json`, and `entry.json`. Private credentials remain separate.

## P1 boundary

The actual portfolio board/cards/tree, read-only hierarchy, overview and complete five-step
editor now live in [Projects.UI](../../src/UI/CanDoItAll.Projects.UI/README.md). Package
controls moved with the board using `ProjectPackageTargetOption`; deletion presentation
uses typed project/participant/recovery targets. `ProjectsPage` retains services, profile
selection, reads, mutation admission, navigation and the original agent-context completion
subscription. It publishes saved summaries and the active Cards/Files/Hierarchy/Overview/
Editor view, rather than treating local edits as canonical state.

The exact Cards/Files filter projection moved to Projects.Contracts without changing its
bounds, ordering or fingerprint. `ProjectsBoard.FilesContent` accepts that projection; the
production host supplies the original Files pane and dialog. Files coordinators, access
state, storage bindings, interaction factories and leases remain in their original owners.
**Files P2 is deferred.** Package import/export retains its existing owner, safety rules,
captured path and inactive, available target profile. No other module extraction began.

Desktop screenshot review reproduced a two-pixel-wide native Files viewer: its enclosing
Stack used start alignment around an inline-size-contained child. The original Files dialog
now uses BaseLib's existing stretch alignment, with a browser geometry/visibility assertion.
This small handoff repair changes no file authority, coordinator, lease or ownership boundary.

The pure project/phase/option/portfolio types moved out of the mixed implementation file,
preserving namespaces, enum values and wire fields. The admitted editor property remains
JSON-ignored. `SaveEditorAsync` uses the original native writer and returns actual accepted
values, submitted row indices and generated IDs after commit. Existing Save/Create/HTTP
callers retain their contracts. The compatible Projects-owned admitted seed port reaches
the same Workbench batch transaction, carrying the original profile/project/lifetime.
SharedKernel gained no Projects dependency. There is no new schema or durable replay engine.

Each editor owns one draft, EditContext, row identities, detached submission and operation
state. Mounted hidden steps preserve raw invalid dates and validation. Input events capture
unblurred Unicode text; both submit actions validate and snapshot before their first await.
Acknowledgement merges only unchanged submitted fields and original live rows. Known save,
seed and deletion facts precede optional reads; unknown outcomes require inspection and
cannot silently replay. Exact cleanup retries remain explicit owner operations. Independent
read generations and owned cancellation keep retired A→B→A reads/completions from altering
a successor. Existing Workbench creation consumes the shared draft/callback seam; its
rendering family and ownership remain otherwise outside P1.

Portfolio readiness and exact editor readiness are independent. A successful catalog refresh
cannot certify a pending or failed editor, while a successfully read root portfolio can publish
its ready context. Files and hierarchy selections read their own summary and never replace the
portfolio snapshot. Route changes and leaving hierarchy retire their auxiliary generations.

## Source and dependency review

The P1 diff is based on the application SHA recorded above, with unchanged Components and
FileTools trees. CI Components development resolves the already-published `4a858412…` repair;
the FileTools CI revision has the same tree as local `3a080ecd…`. No sibling publication or
Dialog repair was needed. Code Analytics, Components and dotnetwatch MCPs were unavailable;
this run used evaluated MSBuild graphs, source searches, CLI builds/watch and Playwright.

The before/after inventory evaluated 179/181 projects. All 37 pre-existing protected UI and
sandbox roots retained their project closures and package references. Web and the Projects
module legitimately gained the new leaf. The leaf closure contains five projects:
Projects.UI, Projects.Contracts, SharedKernel, BaseLib and Common. The independent sandbox
adds only itself. Negative boundary tests reject a forbidden transitive dependency and an
unresolved edge, and inspect exposed contract types. No Foundation, MAF or AppComponents
project gained a product UI reference.

## Desktop and development-loop evidence

All new browser work uses 1920×1080. Source and independently published Production sandboxes
use the real BaseLib widgets, material font, Web theme content and scoped styles, with no
backend registration. The published host loaded all four stylesheets and fonts, retained
all five mounted steps and saved through its explicitly synthetic scenario store. Reviewed
scenarios include large bounded portfolios (202 records), filters/hierarchy, held reads and
saves, invalid off-step dates, rejection, unknown seeding and exact partial-cleanup notices.
Its Files slot and package effects are explicitly simulated, not native authority proof.

Three observed edit-to-render samples per scenario, in seconds:

| Host | Razor | C# | Scoped CSS | Owned JavaScript |
|---|---|---|---|---|
| Original Web | 31.346 / 13.883 / 13.801 | 0.959 / 2.913 / 2.560 | 21.275 / 17.401 / 17.834 | No feature-owned JS |
| Extracted Web | 8.467 / 5.916 / 5.414 | 2.247 / 0.947 / 1.648 | 10.222 / 33.695 / 13.771 | 2.527 / 2.191 / 2.369 |
| Isolated sandbox | 54.260 / 14.235 / 13.652 | 0.571 / 0.600 / 0.751 | 1.711 / 1.323 / 1.494 | 5.205 / 1.018 / 0.868 |

Warm restore/build, simultaneous owned integration validation and a small sample make these
observations unsuitable for universal speedup claims. Browser hydration ranged from
3.163–18.722 seconds on original Web, 1.396–2.518 on extracted Web and 1.088–2.632 on the
sandbox. Actual watch inputs were 4,585 / 4,597 / 300 respectively. Web remains a large host;
the independent six-project sandbox is the isolated development boundary. All probes restored
their exact bytes and recorded SHA-256 hashes. Failed probe attempts remain separately marked.

The original comparison used an owned archive of the starting SHA. Windows path-length
errors in the first deep archive and parallel build failures were retained, then resolved
using a short owned path and serial build. Template copying was disabled only for the
development-loop processes. Extracted polling watch stalled enumerating directories (verified
with a method-only stack); ordinary Windows file watching then served the host. SDK static
asset parsing warnings remain recorded. No SDK, product watch policy or normal user host was
changed. New JavaScript probes observe the browser reload and interactive readiness explicitly.

## Validation status

The final focused run passed all 37 Projects page/native mutation cases, 11 leaf form/boundary
cases and six existing context/CRM consumer cases without skips. Three added final-review
controls first reproduced stale auxiliary snapshots and a root context left loading, then
passed with the independent read ownership described above. The readiness marker now observes
the first interactive render without requiring bUnit renderer metadata.

The 1920×1080 production Files/shell journey passed on the final host and visible-viewer source. Deterministic
Agent and Workflow consumers use the same actual five-step UI-created project and verify
native IDs, grants and bytes. Earlier mixed attempts, the subsequent narrow follow-ups and
their limitations remain separate in the [evidence index](projects-portfolio-ui-p1-evidence.md).

Portability-static passes without `--write-baseline`: 15,216 reviewed executable-source
findings. All 15 added and 13 stale baseline entries were inspected: moved logical-name/node
comparisons, removed error-message prefix checks and four Markdown fence false positives.
No scanner rule was weakened. The scan includes all 7,718 tracked candidate files.

The full frozen Stable checkpoint remains **mixed: 15,905 passed, one failed, zero skipped**
across 20 assemblies. Its only failure was the source secret scanner finding synthetic
scanner-theory arguments in the generated discovery list. The raw list is retained under
the existing `.artifacts` policy; three known synthetic display arguments were normalized
in its shareable derivative, with hashes preserving provenance. No scanner, test or product
code changed. The separate complete owning Unit checkpoint then passed all 9,400 cases
on the same frozen binaries; this closes the affected proof without relabeling the full run.
Original discovery listed 15,851 cases; the 55 additional runtime theory rows
are reconciled by method, with no missing or unexpected methods. The checkpoint's 206
binary/PDB identities remained unchanged. Later host/readiness,
Files alignment and control changes have separate final focused/browser proof; the Stable
binaries are not claimed identical to the final checkout. The historical R2 Stable checkpoint
and mixed browser report remain unchanged. No paid live-model request or exhausted-journal
reset was authorized or performed.

## C# Architecture Gate Result

Status: Pass with follow-up

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Informational | S0 revalidated without explaining the historical selector anomaly | Exact cold acceptance and six controlled ordering cases | Retain the qualified disposition; reopen if the configured anomaly recurs |
| Informational | Files rendering and its authorities remain an intentional production-host boundary | Typed Files slot, native handoff and file-owner tests | Keep Files P2 deferred to an explicit later task |

### Dependency direction

The evaluated five-project UI closure and six-project sandbox closure match the declared
boundary. All 37 existing protected closures and package sets are unchanged. The typed
admitted seed port belongs to Projects.Contracts and resolves to the existing Workbench
owner in the composition root. No implementation service, service locator, EF type or
Infrastructure profile crosses into the renderer. Code Analytics was unavailable; evaluated
MSBuild references and the negative graph controls provide the recorded fallback.

### Partial-class policy

No new partial class hides an extracted responsibility. Existing Projects and Workbench
owner partials receive the minimal acknowledgement/seed adaptations. Their transaction,
authority and compensation responsibilities remain with those owners. The actual rendering
files moved to the leaf rather than being duplicated behind forwarding components.

### Testability proof

`ProjectEditorDraft` owns field revisions, row identity, detached submissions and reconciliation.
The modal owns form validity/focus; the routed page owns service effects and view lifetimes.
Safe package and cleanup records describe presentation without owning recovery or transfer.
Pure renderer/draft tests run without the original Web host. Native mutation tests use actual
PostgreSQL and held owner/commit boundaries, including lifetime refusal, later input, exact
cleanup and no seed replay. Production browser checks separately prove composition and bytes.

### Closure decision

P1 is complete: the code, final focused/native/browser proof, completed frozen Stable run
with its separately repaired full Unit checkpoint, and required static/documentation gates
support the same boundary. The original mixed Stable result and S0 causal uncertainty remain
explicit. Files P2 and other module extractions require a later task; this result does not
certify application release readiness.
