# Manager Summary and Activity — complete rendering, original query semantics

Read [S13–S18] in the [source register](SOURCES.md). This is existing Workbench code
selected for extraction, not a regression attributed to WB1. Tests should first
characterize current outcomes; repair the precise inconsistent paths below.

## Separate four states

1. Local draft options the operator is choosing.
2. The accepted report: exact options, resolved scope, time window, generated time
   and native values. It can remain visible as explicitly stale data for that scope.
3. A particular preflight/load attempt, including pending large-scope confirmation
   and progress/error ownership.
4. A particular Activity or warning dialog over a particular accepted report.

Mounting Summary, switching to its tab and changing options must not load report
payloads. Default options remain HistoryOnly / CurrentProject / Month. Load performs
native scope resolution/preflight and then the original native query. Preserve the
existing 25-descendant confirmation policy and separately configured hard limits;
use current named owner constants, never a duplicated permissive UI cutoff.

Confirmation is for the captured scope and mode that were shown. Options/root/profile
changes retire that confirmation; stale Continue must not load the replacement
scope. Do not allow “confirm once” to authorize all later scopes. Distinguish an
explicit confirmation warning from the hard limit that remains a refusal.

The query owner resolves `AsOfUtc` and `HistoryFromUtc` for the accepted report.
All activity pages and accepted charts refer to those same values. A new Load is a
new report, not an in-place rewrite of the historical result. The All option keeps
its existing chart-series bound rather than loading all transcripts or rendering
unbounded per-event points.

## Preserve accounting/report meaning

HistoryOnly still obtains the schedule-only plan facts. HistoryAndFuture obtains
remaining expected costs. Historical known, historical estimated, future remaining,
unknown pricing and non-USD future currencies stay separate. Existing “Committed”
math is historical known plus future planned; do not silently add historical
estimates again. Keep native duration/row/coverage rules and non-overlapping Agent,
standalone Workflow and root Process sources. UncategorizedAgentActivity is the
existing Agent-only path: do not broaden it to unrelated SimpleChats/process runs.

Do not turn missing/failed/unloaded totals into 0. Genuine accepted empty results
can show zero. Retained same-query stale values need a visible qualification.
Charts, tooltips, warnings, ratio denominators and “as of” labels must all reference
the accepted snapshot. A draft period changed from Month to Day must not reinterpret
an old Month chart via new axis options. Bind queued child-content templates to
local accepted values, not mutable fields that a later render can clear.

Reporting read success is not authority to open conversation content. Keep this
screen's current feature set: the Activity table is a metadata report, not a new
transcript browser. Where existing navigation is offered, native owners recheck it.

## Concrete baseline risks to cover

In the current Summary panel, progress and failure paths are not fully bound to the
originating attempt; scope resolution can return into a newer panel before its
confirmation state is installed. The successful final load has a cancellation
check, but that is not coverage of all intermediate results [S13]. Capture project,
profile/lifetime context, immutable options and receiver before the first await.
Check them after every relevant await and in progress/error/finally paths. A late
failure may be retained as old diagnostics, never overwrite B's visible state.

The retained StateStore keys profile/project, with a capacity of 32 [S16, S28].
Keep deliberate retention of options and last settled snapshots across tab re-entry.
Do not store busy flags, CTSs, open overlays or mutable in-flight drafts in a shared
retention entry. Two mounted panels must not mutate each other's pending options,
confirmation or active report. Resolve the retention/per-view distinction with the
smallest explicit change, preserving compatibility tests and bounded memory. Read
profile identity from the actual native accessor; do not conflate a selected future
profile with the running host's canonical database. Retire held data on relevant
actor/profile/project-lifetime changes without a new authentication scheme.

Dispose cancels reads, detaches subscriptions and retires callbacks. The request
releases its own cancellation source after its native work unwinds. Do not hold
blocking waits on the renderer or reuse disposed sources. Keeping a cached report
must never relabel it as belonging to a recreated project lifetime.

## Entire Activity dialog

Use actual Agents, Simple chats, Workflows and Processes options, outcome filter,
all-match totals, current-page rows, Previous/Next and the exact PageSize behavior.
While it is open, existing filter changes trigger their bounded query. Opening a
new accepted summary or reopening the dialog gets a fresh origin. A report reload
must not silently bind an old cursor to the new report.

The current implementation assigns `page` after awaiting a query and then indexes
aggregate cache with live filters; it initializes only once [S14]. Replace these
implicit lifetime assumptions, not the native query protocols. Cache identity must
include the accepted report/window/scope and requested kind/status. Process
continuation tokens remain opaque native data. Do not convert keyset paging to
integer offsets or reconstruct a Process cursor in the renderer. Other existing
sources retain their actual offset behavior.

The total count, duration and cost are for *all matching rows*, not only the displayed
page. Reuse known aggregate only for that exact request family. Page back/forward
must not extend AsOfUtc, accumulate duplicates or mix actor/project contexts.
Any read that resolves after Close or A-B-A replacement cannot fill the new cache,
change its busy state, open a warning dialog or claim a newer error.

## Required report proof

Use seeded native histories plus a bounded actual Agent/SimpleChat/Workflow/Process
journey where each source already supports it. Native replay/authoring is not part
of reporting. Cross-check exact native IDs, aggregates, time cutoffs, descendant
membership, incomplete indexes, estimates, known-zero and other currencies. Exercise
more than one page for each paging family and independently change two mounted
views. Keep original native calculators/readers as the oracle; a fake echo of the
renderer request proves only UI behavior.
