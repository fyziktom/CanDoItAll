# Scheduler UI boundary and validation

Scope: bounded Plugins PL-R1/PL-R2 corrections, then the complete SchedulerPlanner
workspace. No third module. Starting checkout: `components-decoupling` at
`0e176a3b99270cdc9a86a57d6276d05979d7352e`. The supplied bundle was the only untracked
entry input and remains unchanged. Execution uses SDK 10.0.303, `SchedulerUiProof`
and source references: Components `f258ab6a959a97fa16c01d0858e7dc122728a11a`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, SharedInfo
`83e21e23bcf43d92b061a6d367ac385241d13cd3`. Components, Code Analytics and dotnetwatch
MCP tools were unavailable; local source, evaluated metadata and CLI watch were used.

## Ownership

| Boundary | Responsibility |
| --- | --- |
| SchedulerPlanner.Contracts | Existing pure enums, summaries, targets, schema/validation records and history query; observed mutation facts |
| SchedulerPlanner.UI | Four tabs, filters/cards/paging, raw/typed form, picker/edit/delete dialogs, real Canvas and per-host JS/CSS; Agent host slot |
| SchedulerPlanner.Presentation | Workspace/draft lifetime and independent reads; input/schema/options session; mutation admission and reconciliation |
| SchedulerPlanner module | Thin route, exact host mapping/profile guard, operator authority capture, real Agent context/discovery/launcher, persistence, Quartz and execution |
| SchedulerPlanner.UiSandbox | Fixed clock, bounded local owner storage, controllable waits, harmless Agent intent, real shared rendering and assets |

The ignored privileged editor authority and legacy Canvas workspace DTO remain in the
production assembly. The UI draft contains only editable values. Existing pure public
names, numeric enums, serialization/defaults and source consumers are preserved through
12 explicit type forwarders. No schema, migration, new scheduling engine, HTTP UI hop, provider,
permission framework, generic effect bus or shared component rewrite was introduced.

Workflow-only production scheduling, original Agent governance and source leases,
optional transaction ordering, saved version/input/authority snapshots, short fire claims
and accepted-run observation remain owner responsibilities. Plan deletion still cascades
display runs and retains independent FireAdmissions; it does not delete Workflow runs.

## Draft, read and outcome policy

Every editable field is captured before validation or authority awaits. Individual field
revisions preserve later edits, including returning to an earlier value. Saved identity is
accepted before follow-up reads. New/reset and modal origins are independent; dirty and
unresolved retired drafts remain reviewable, bounded at 16. Admitted writes are observed
after retirement without closing or resetting a successor.

One mutation is admitted per plan/new draft, with handler-side save/pause/resume/delete
conflict checks. Independent plans can progress. Unknown keeps replay locked. Recovery
reads an explicitly supplied exact identity or exact flag/deletion state, never a name
match. Operator review describes current persistence, not proof of the earlier request or
Quartz projection. Settled notices can be acknowledged; receipts are bounded at 128.

The owner records Persisted only after the durable boundary returns. Projection, reload,
logger and post-commit disposal failures retain the exact fact and primary exception.
An ambiguous commit acknowledgement remains Unknown even if a separate observer sees a
row. ProjectionSynchronized is distinct; incomplete facts do not invent current next-fire
values. Managed Agent tooling records the known effect before propagating follow-up failure.
A no-op enabled-state request keeps its original behavior and does not recheck projection.
Refresh never synchronizes, reauthorizes, launches or redispatches.

Malformed/non-object/empty JSON stays visible and prevents a write. Typed edits preserve
unknown JSON, whitespace and incomplete numbers. Exact-schema defaults are displayed and
owner-normalized. Missing selected IDs remain visible. Options cache only the current
descriptor/dependency context; stale success/error/finally paths are fenced. Missing
providers fail explicitly; an available empty result is distinct. CTS resources remain
owned until continuations unwind.

Calendar display timezone is independent of schedule timezone. Bounds remain history 50,
clamp 1..250, planned 30 days/max 18 per enabled plan and combined 160 events. History is the
bounded query's recent overlay, read-only and never inferred into a plan by title. JS
registration belongs to one live host/key, with retirement and disconnected disposal.
Selection uses the exact current surface event ID carried by the shared calendar state
callback; the shared optional-date event payload can deserialize as null. No title or
history ID fallback is used. Capture-phase focus prevents a first-click scroll from moving
the second click onto another event. View/date/timezone/selection and schedule filters
survive tab remount; delayed callbacks cannot reopen a retired dialog.
The production Agent retains its managed identity, source/route, three protocol view tokens,
selection/overlay facts, readiness and OnSuccessfulRun completion mode.

## Evidence ledger

Task-local transcripts/TRXs are under `artifacts/scheduler-ui`, browser images/owned host
logs under `output/playwright/scheduler-ui`. The ignored LF-normalized input-package copy
passed root validation (36 files, 56 links, 5 JSON, 35 registered entries), root utility
tests 11/11, shared validation and shared utility tests 14/14. Input CRLF files were untouched.

| Requirement group | Current proof | Status |
| --- | --- | --- |
| V-PL-01..06 | Failing-first catalog/double-fault/popup cases; fresh light 50, owner 11, pages 9, browser 2 | Passed 72; previous 160 Plugins/21 TestLab historical |
| V-SC-01..20 | Light renderer/session, calendar lifetime and negative dependency tests 44; real page 15 | Passed 59 |
| V-OW-01..06 | Real PostgreSQL sync/reload/log/lost acknowledgement, flags, cascade and retained admissions | Passed 8 within integration 59 |
| V-OW-07 | No projection repair introduced | Not applicable |
| V-OW-08..12 | Unit 53: forwarding, Agent acknowledgement, installed Quartz DST/misfire/bounds; integration 59: source authority, original fire snapshots and read-only observation | Passed 112 |
| V-BR-01..09 | Actual Web owner journey; native sandbox double-click, empty/history, two hosts, Month remount, delayed import, held newer input and bounded large state | Passed 2 |
| Graph/build | Web 140 to 143 projects,140 packages/30 native unchanged; sandbox 14 projects/1 framework asset package/0 native; zero cycles/unresolved | Passed; eight direct roots built |
| Published assets | Standalone Production sandbox, native exact-plan edit, four tabs, unknown outcome;51 responses, no console/page/asset errors; scoped min-height 608px | Passed |
| Repository closure | Complete proposed-tree scan,34 added/27 stale occurrences individually reviewed;15,131 protected findings;6 portability/4 secret-tool/9 documentation-evidence self-tests; canonical docs; secret scans; whitespace | Passed |

All nine selected lanes have build-backed discovery equal to expected and executed counts:
Plugins 50/11/9/2 and Scheduler 44/15/53/59/2, **245 distinct selected cases**, zero failures
or skips in the closing runs. This is a bounded selection, not the full Stable suite.
`artifacts/scheduler-ui/requirement-evidence.json` maps every one of the 47 matrix IDs to
actual expanded test names, owning lane, source baseline, expected/discovered/executed
counts and TRX/discovery paths. Earlier failed probes remain distinct receipts and are
not counted green.

The reproducible commands are `dotnet test <owning project> --configuration SchedulerUiProof
--list-tests --filter <selection> /m:1`, followed by the identical filter with `--no-build
--no-restore --logger trx`. Scheduler selections are all tests in the light project,
`FullyQualifiedName~SchedulerPlannerPageTests` in Components, `FullyQualifiedName~Scheduler`
in Unit, and `FullyQualifiedName~SchedulerBrowserTests` in Playwright. Integration selects
`SchedulerFireAdmissionPersistenceTests`, `SchedulerWorkspaceOwnerPersistenceTests`,
`SchedulerSourceAuthorityPersistenceTests`, `SchedulerUiOwnerReceiptTests`, and
`Scheduled_Workflow_read_keeps_original_fire` (OR of FullyQualifiedName contains filters).
The isolated PostgreSQL connection and configuration are described in [Testing](../testing.md).

Direct restored incremental builds used `dotnet build <project> --configuration
SchedulerUiProof --no-restore /m:1` for Contracts, UI, Presentation, sandbox, Plugins,
SchedulerPlanner, Composition and Web. Their elapsed milliseconds were respectively
1335.4499,2038.3706,2161.2320,2517.7368,4179.4617,5797.6031,18524.5101,18143.8948.
Final view-state fixes were rebuilt by light/browser discovery and a final Web build and
sandbox publish. Existing unrelated analyzer warnings are retained; final Web had none.

Broad Stable, live providers, other module lanes and non-Windows runtime lanes were not
selected. The dependency additions and solution/CI inventory entries are feature-local;
no shared shell, root build policy, serialized shape, persistence mapping, migration or
common test-host behavior changed. Pure public-type relocation and owner-stage changes
explicitly widened proof to the 59 Scheduler/Workflow authority/fire integration cases and
53 Unit consumers. This is not merge/release closure or a frozen shared checkpoint.

## Development loop

Milliseconds, Windows source dependency mode, SDK 10.0.303, restored warm outputs,
1600x 1000 Chromium viewport; hosts ran sequentially. Background machine load was not
controlled. Original and extracted Web use the same harmless Start/End Workflow with
one enabled and one paused plan and one display-history row. Sandbox has two plans and
seven bounded display-history status fixtures. It runs no database, Quartz hosted scheduler,
Workflow or Agent runtime. These are machine-specific development-loop observations.

Original Web startup observations were 52981.3658,45693.6687,44845.4588 across three owned
launches; extracted Web 51960.9121 and sandbox 10733.6309 each have one startup observation.
Startup is separate from the three samples per edit class below. Initial baseline CSS
readiness and clipped-corner JS harness failures are retained and excluded from successful
samples. No runtime/native asset failure remains in the closing browser or published proof.

| Change | Original Web samples (median) | Extracted Web samples (median) | Sandbox samples (median) |
| --- | --- | --- | --- |
| Razor |9003.2473 /6971.2081 /5968.8347 (6971.2081)|2929.5208 /1921.0929 /906.4440 (1921.0929)|2913.5399 /376.8604 /390.2103 (390.2103)|
| Executed C# |884.0500 /1193.8708 /1146.5108 (1146.5108)|1500.1877 /1411.3072 /1432.4125 (1432.4125)|325.2507 /297.4654 /264.4200 (297.4654)|
| Computed scoped CSS |7256.8750 /7222.5332 /5538.3631 (7222.5332)|7043.0680 /5330.0816 /5079.0051 (5330.0816)|857.0481 /1331.3323 /667.4483 (857.0481)|
| Executed JS after refresh |1329.0608 /1245.2170 /1169.4163 (1245.2170)|842.9850 /728.8718 /600.7963 (728.8718)|305.7534 /234.0079 /178.9161 (234.0079)|

Razor and C# probes were applied by hot reload; CSS used static-asset/scoped-style updates;
JavaScript required refresh and an executed host callback marker. No restart duration was
mixed into an edit median. The final filter/calendar state retention fixes do not alter the
probed paths, the dependency boundary or asset inputs; they received fresh light, browser,
Web build and published-host proof. No full-Web startup or C# acceleration is claimed.

Actual watch inventories contained 4424 original Web,4452 extracted Web and 640 sandbox
entries when measured (one additional UI filter state source was subsequently added).
Required child Razor, C# presentation, scoped CSS, host-local JS, shared Canvas source and
authoritative Web `output.css` inputs are present. The theme is an explicit content input,
not a Web project dependency; the sandbox README gives its rebuild command. Assets include
BaseLib theme/material font, Canvas runtime accessibility and calendar scripts, the host's
generated scoped stylesheet, and the intentional new
`_content/CanDoItAll.SchedulerPlanner.UI/schedulerPlannerCalendarInterop.js` URL. Old module
script references were removed. Published fingerprinted URLs and native mouse editing passed.

Option-budget tests demonstrate that repeated ordinary text edits cause no independent
option-source reads. Dependency changes request only their affected source; cache entries
are per draft/descriptor/current dependency and retire with the draft. There is no global
cross-circuit cache or debounce timer hiding owner effects.

## C# Architecture Gate Result

Status: **Pass**. No blocking finding remains.

| Check | Evidence |
| --- | --- |
| Responsibilities | Route owns host/Agent composition; renderer types own their actual DOM; workspace owns lifetime; input session owns schema/options; mutation coordinator owns admission/reconciliation; owner keeps durable effects |
| Dependency direction | Evaluated graphs have no cycles/unresolved edges; light public/runtime closure tests pass; no EF/Quartz/runtime/provider/Web edge in UI or sandbox |
| Partial classes/construction | No production partial cluster added; explicit owner ports and ordinary DI; no new service locator, generic bus or parallel implementation |
| Independent testability |44 light cases run without the original application;59 real PostgreSQL cases independently prove durable/authority behavior; route/native browser/publish proof remains separate |
| Extension seam | Both real route and sandbox use the shipped workspace renderer and presentation policy; future Scheduler rendering uses this seam |

## Cleanup and closure

Owned PostgreSQL 18.6 container
`c9ce2d7ff9016c0d0942cfa02121123bfacc8140b0ecb6b1d2835437bfffc166`, named
`candoitall-scheduler-ui-proof-20260928`, label `candoitall.task=scheduler-ui-proof-20260928`,
loopback 63540, had no remaining fixture databases before stop/removal. Six recorded watch /
published host PIDs are gone; browser fixture hosts dispose their owned process handles.
All five measurement files passed exact SHA256 restoration. Sibling HEADs and clean tracked
states are unchanged. Ordinary application/database port 5032 was never reused or stopped.
No remote push, merge, release or deployment occurred. Local `dotnet publish` was packaging
proof only. The final signed commit is reported with the delivery; its exact SHA is also in
`artifacts/scheduler-ui/closure.json` after commit.

S0 and the complete Scheduler slice are closed by the evidence above. Remaining policy is
intentional: refresh is read-only; unknown outcomes require explicit exact-plan review;
projection warnings are acknowledged, not silently repaired; history/calendar coverage is
bounded. Real provider/Agent execution and other modules are outside this assignment.

## SC-R1 / SC-R2 closure (Memory handoff prerequisite)

The Memory assignment reproduced both source findings against `059f4dafcdfc4ae65bbb13c1533ce8a49d988652` on `components-decoupling`. Two independently controlled exact-ID reviews exposed stale success, mismatch and error publication; an optional dependent node retained an obsolete diagnostic after changing project. The new 14-case failing-first selection failed 10 and passed 4 before repair.

Review requests now own a replaceable cancellation/read lifetime and compare the exact originating receipt and submission before publishing. Older completions cannot remove a successor submission, clear its admission or change a disposed view. Flag/deletion review uses the same policy. Both real review controls display their current request as busy. Parent changes retire only diagnostics belonging to explicitly cleared dependent inputs; unrelated and raw parse errors remain blocking, and missing required input still reaches fresh owner validation.

Current source-mode `MemoryUiProof` proof: `SchedulerReviewRegressionTests` expanded to 15 cases including the rendered review control; the complete light topic passed **59/59**, `SchedulerPlannerPageTests` **15/15**, and `SchedulerBrowserTests` **2/2**, zero skips. Direct UI, Presentation, module and Web builds passed. Browser proof at 1600 × 1000 covers the real production journey and a held exact-ID review through retained-draft controls. The inspected pending-review image shows disabled/busy recovery, usable dialog close and correct modal layering. Two preceding browser failures were invalid test attempts to operate a modal-blocked toolbar and a locked normal Edit control; the final journey uses the actual retained-draft path.

Commands use `dotnet test <owning-project> -c MemoryUiProof --list-tests --filter <topic> /m:1`, verify the stated counts, then the identical filter with `--no-build --no-restore`. Logs/TRX are under ignored `artifacts/memory-ui/s0-*`; images under `output/playwright/scheduler-ui`. Full proposed-tree portability enforcement passed without baseline writing (15,131 unchanged reviewed findings). Owner/fire/authority/Quartz and prior Plugins source are unchanged; those historical tests are not represented as rerun. The separate task-owned PostgreSQL 18.6 server is on a random loopback port; the ordinary app/database is untouched. Memory execution continues after this bounded prerequisite.
