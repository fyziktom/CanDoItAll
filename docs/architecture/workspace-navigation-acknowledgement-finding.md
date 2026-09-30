# WCL-NAV1: navigation acknowledgement outlives the observed browser sequence

Status: **OPEN, P2**. Attribution is unproven. This finding blocks composed-application
readiness and leaves WC-C3 shared navigation closure open after its controlled repairs.

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
