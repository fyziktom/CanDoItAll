# WCL-NAV1: navigation acknowledgement outlives the observed browser sequence

Status: **Closed as an R2 test-completion correction**. New tests
intercept the real framework dispatcher and independently hold only the navigation JS
acknowledgement. They retain the actual callback and connection identities. The browser
route and both usage charts are visible before that acknowledgement reaches the server.
An active connection and an abruptly disconnected retained circuit both reproduce the
normal near-60-second `TaskCanceledException`; permanent circuit retirement takes the
framework's explicit session-ended path. Releasing the acknowledgement produces the real
server `NavigationCompleted` event. The original historical caller was not instrumented,
so this evidence proves the mechanism without retroactively inventing its correlation.

The usage journey now requires both its actual successful JS acknowledgement and the
matching framework completion before disposing its context. A second request with the same
URI makes attribution fail rather than borrowing another view's completion. The complete
shared fixture logs remain asserted. The correction changes test observation, not production
navigation, JS timeout, circuit retention, unsaved tabs or the prior C1/C2/C3 lifetime fences.

Ignored R2 evidence: `artifacts/workspace-closure-r2/20260930-6b05246f7/nav-corrected-01`
passes eight discovered cases (three controls and all five original consolidation cases).
`nav-permanent-02` separately proves permanent retirement. ABA navigation and two independent
contexts are covered; closing the first does not break the survivor. The first permanent
control used a deadline shorter than the framework's unchanged interop deadline and failed;
that attempt is retained as `nav-permanent-01`. Those were interim controls. The final composed evidence below completes the
required Development and published-host proof without changing the original failure.

## Final R2 composed closure

The fresh complete browser inventory on application
`f09a44a196ea139d1d37cb3ce2bcb33c6cdffd91` and Components
`4a858412d2c2a3f6123bf23d8c4584f05b47627d` passes all four acknowledgement controls,
all five consolidation cases, the shared-host lifetime sequence and Collaboration's unchanged
whole accumulated-log assertion. The reconciled full inventory separately retains an unresolved
shared-provider setup/model-selector failure and external prerequisites. That ordinary failure
does not occur in the NAV1 controls and is not replaced with a focused pass.

The separately published .NET 10.0.12 Production host records seven actual navigation requests,
seven released successful acknowledgements and seven matching server completions. The held
acknowledgement proves a visible route can precede framework completion. ABA visits retain
their individual callback/connection identities. Permanent retirement of the first circuit
is observed in its server log, and an independent second circuit completes navigation after
that retirement. No NavigationFailed or unhandled circuit exception is recorded. Initial
historical antiforgery errors remain in their original checkpoint logs; the final shutdown-inclusive
log review records the current hosts separately without suppressing errors.

`production-navigation-verification-05.json` binds the actual callbacks, circuits, URI counts,
timestamps and complete log hashes. `frozen-browsers-05/browsers.trx` retains the fresh broad
run; `browser-semantic-final05.json` reconciles its separately configured prerequisites.
The separate `navigation-overlap` proof holds both real callbacks across A→B→A before release,
then verifies that the successor view remains unchanged and a second context navigates after
the first closes. All three callbacks match server completions; the complete Development host
log contains no navigation, circuit or disposal failure. Its copied assemblies match the
frozen output, and the extra control does not relabel or replace the full browser inventory.
The final Stable gate passes with the prior C1/C2/C3 owners intact. This closes the demonstrated
observation defect; it does not invent caller identity for the uninstrumented historical event.
The historical continuation below is preserved as investigation history.

## Original observation and ownership

Application `3d7c88f384b7920744464a7c7570530ca825325a` consumes signed Components
`22d5b21afdf80c2bca74c1c598f0b1bb72c86f9e` in source mode. The frozen Windows Release
browser run uses private PostgreSQL 18 and .NET 10.0.12. Host 27108 logs navigation to
`/agents` failing at **2026-09-30T17:23:51.3556293Z**, then CircuitHost logs an unhandled
TaskCanceledException for circuit `I66cc-JWOsbz1SmiyNFTweP4eo_tGM8dIlOhz0XX9-k`.
The stack ends in `RemoteNavigationManager.NavigateToCore.PerformNavigationAsync`;
it does not identify the application caller or contain the original WC-C1/C2/C3 stacks.

The Collaboration UI and real owner persistence assertions complete before the unchanged
full-server-log assertion rejects this earlier failure. Its thread and messages are retained
in the raw owner evidence. No failed business commit, rollback or replay is inferred.

## Known ordering and missing causality

`AllUsageScopesDriveChartsAndDialogs` finishes at 17:22:51.3558113Z, **59.999818 seconds**
before the error. Its last actions select Both, observe `/agents` and existing charts,
then dispose the browser context. This makes an unacknowledged navigation during teardown
a useful hypothesis, not a circuit-to-test mapping. Prompt Gallery is running when the
delayed error is logged and cannot be assigned causality from that timestamp alone.

The [.NET 10.0.12 framework implementation](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Components/Server/src/Circuits/RemoteNavigationManager.cs)
starts an internal asynchronous operation from `NavigateTo`. It awaits JS completion and
contains cancellation only after permanent disconnection; other exceptions raise the
circuit error event. The original trace lacks request/completion correlation and the
application admission time. A visible URL does not establish that this operation completed.

The original WC-C3 repair fences MainLayout work before and after every await, releases
its exact listener and rejects unacknowledged browser-state writes. The remaining question
is whether navigation was admitted by a retired application owner or was still active when
the framework lost its acknowledgement. Changing either boundary without that evidence
could hide active failures or redirect a successor circuit.

## Investigation and disposition

Six unchanged focused cases pass on another Windows host: all four Prompt Gallery cases,
Collaboration and the failed note case. Eight actual usage/dialog/context-close repetitions
on each of the final Windows and Linux published hosts produce no matching error after
the final sixty-second observation interval. Those private Production fixtures differ from
the original Development collection host and its accumulated history. These passes do not
close the original broad failure or make its log clean.

Continue from the retained original identities: correlate the initiating renderer,
framework request/completion and circuit connect/disconnect in a bounded test host; hold
actual navigation acknowledgement independently of URL visibility; then retire that exact
circuit with an active-timeout negative control. Apply an application admission correction
only if its owner violated retirement. Otherwise isolate the framework lifecycle boundary
before selecting a remedy. Any production change requires affected composed-host and
Stable proof. No timeout/retention change, replacement NavigationManager, log allowlist or
successful-Save fallback was introduced during this investigation.

The ignored evidence root is `artifacts/workspace-closure/20260930-d9273a889`.
`navigation-finding-final.json` records hashes for the original full TRX/log, six-case
investigation, both eight-sequence records and retained logs. The original full browser
checkpoint remains failed. See the [closure report](workspace-critical-fixes-closure.md).
