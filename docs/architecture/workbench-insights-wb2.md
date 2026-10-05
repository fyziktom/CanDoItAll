# Workbench Insights WB2

Execution is in progress. This record does not claim closure of WB2 or readiness for
the next slice. The sealed package is
`codex/bundles/CanDoItAll_Workbench_Insights_WB2`; original attempt logs and manifests
are retained under ignored `artifacts/workbench-insights-wb2`.

## Entry and boundary decision

Entry main is `23393e402cc2f4c48d1338a7602586461f00ff73`; its changes after the
reviewed WB1 checkpoint only add the WB2 package. Components entry is
`dc573e2b438621599401a28968acef3682d14e63` and FileTools is
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. All three checkouts were clean before
the new regression tests. The previously completed Agents, Workflow and WB1
boundaries remain preservation constraints.

CodeAnalytics, Components and dotnetwatch MCPs are unavailable in this session.
Source inspection, evaluated MSBuild restore graphs, CLI builds and actual
Playwright browser interactions provide the corresponding evidence. Entry graphs
and native/planning watch file sets were captured before moving renderers.

The new `CanDoItAll.Workbench.Insights.UI` leaf will own the full Manager Summary,
Activity, Selection Panel, Object Index, Signals, Canvas Health and advanced detail
renderers and their CSS. BaseLib, CanvasLib and Charts remain the owners of their
real child widgets and JavaScript. No main Structure canvas or Processes renderer
is included in this extraction.

The native Workbench module will retain report scope resolution, bounded queries,
arithmetic, runtime profile selection, process cursors, access checks and all
project/task/file/runtime effects. A small Insights.Contracts assembly will hold
only shared report values; native preflight, process cursor and mixed workbench
model types stay with their existing owners. Public moved values retain their
namespaces and explicit type forwards.

Each view gets its own session and cancellation lifetime. Settled report values
and options may be restored from the bounded profile/project store, but pending
drafts, confirmation openings, activity cursors and in-flight work cannot be shared
between panels. Report charts and activity use the accepted report's exact options
and time window. Typed projections and captured origins cross the UI boundary;
native entities, services and raw metadata do not.

Selection and support actions capture the project lifetime, selection generation,
exact target IDs and offered action before dispatch. The native adapter rechecks
that origin and passes the original admission to mutation owners. Current selection
must never substitute for the targets of an old callback. File preview, Mermaid,
task editing and runtime execution remain explicit native actions.

A new independent Insights sandbox and test project will exercise the same real
renderers without constructing the Workbench module. This project boundary is
needed because moving files inside the module would retain its Foundation, MAF,
Processes and application dependency graph. A generic service bag, new parent
partial-class facade or wholesale move of mixed contracts would not establish the
required boundary.

## Checkpoints and proof plan

1. S0: failing-first Gantt cleanup, exact shared owner repair, source/published/native
   close/remount proof, signed Components checkpoint.
2. W1: complete reporting and Activity, independent stale-result and lifecycle
   tests, native arithmetic/preflight/cursor proof, signed checkpoint.
3. W2: all selection/support renderers and native captured actions, child and
   mutation/refusal/partial-result tests, signed checkpoint.
4. W3: Fast/Parity source and publish, three reversible watch samples per owned
   file kind, native operator/Agent/file/Workflow/Scheduler/multi-instance campaign,
   final dependency/census/static gates and signed closure.

All new visual validation uses 1920×1080 at device scale 1. New owned resources use
the WB2 artifact root and isolated build configurations; the ordinary application
at port 5032 and historical fixture data are not used. No paid model calls, pushes,
merges or releases are authorized. New shared contracts, solution/CI registration
and native composition changes are the named trigger for one broad Stable gate
after the final source set is frozen. Earlier checkpoints use focused owning tests.

## S0 observations

The first helper attempt incorrectly read a nested interop field and is retained
as a failed proof attempt. With that helper corrected, unmodified production code
failed eight of fourteen new cases: six pending/already-completed cancellation and
fault cases, combined update/cleanup failure, and independent chart cleanup.

Disposal now observes the pending operation, independently attempts cleanup and
preserves the original exception (both exceptions if cleanup also fails). Actual
browser removal also reproduced a null element reference during delayed disposal.
A per-instance identity now releases the acquired JavaScript chart after its DOM
element has been removed. The callback reference is released in the outer finally.

All 102 tests in the Gantt owner assembly pass, with build-backed discovery and
pre-run DLL/source manifests. Source Fast, independently published Parity and the
real native WB1 host pass held-update close/remount. The two-chart sandbox leaves
the other chart usable and applies the newest queued scale. Table/canvas task
identities and dimensions agree. Browser screenshots were inspected at the stated
desktop size. Served Gantt JavaScript bytes match the changed Components source
across all three hosts. Initial portability enforcement passes with 15,227 reviewed
findings unchanged. These results establish S0 evidence only; W1–W3 remain open.

## Reporting checkpoint

Insights.UI owns the complete Summary and Activity renderers and their per-view
sessions. The small Insights.Contracts assembly contains only reporting values;
native scope resolution, analytics, arithmetic and Process continuation protocols
remain in Workbench. The host retains settled snapshots by the original bounded
profile/project store and checks actor and project lifetime before restoring them.
Each scope admission is checked before and after native reads, including descendants.

Draft changes leave accepted chart scales, cost meanings and report cutoff intact.
Unloaded Activity totals display an em dash. A failed reload retains and qualifies
the accepted values. Each opening owns its native cursor/aggregate cache; replaced
read progress, errors and finally blocks cannot publish into the current view.
The completing operation releases its cancellation source after unwinding.

At this checkpoint, 13 independent reporting cases and 11 native component cases
pass with fresh discovery and pre-run source/DLL hashes. The real application
reports the existing task's 6.5-hour schedule and separate EUR future cost and
preserves its report over tab disposal. The independent large-desktop browser
checks all four Activity types, 20-row pages, 43 all-match and 11 filtered totals,
fresh reopening, delayed replacement, confirmation and retained failure state.
Four actual chart instances render across two independent views, without page errors.
Screenshots were inspected. Native all-source arithmetic, cursor/as-of continuity,
publish, watch and final multi-instance journeys remain part of W3; this checkpoint
does not close those proof groups or the WB2 family.

The four added portability findings are the sandbox's named watch iteration and
optional ownership diagnostic configuration reads. They are portable and match the
existing sandbox convention. Two further findings match the README's PowerShell
code-fence label; its commands require no platform-specific behavior or elevation.
The reviewed baseline has 15,233 findings and final no-write enforcement passes.
