# WC-C3: initial tab tracking persists after circuit disconnection

## Closure update — 2026-09-30

Status: **original WC-C3 controls repaired; shared navigation closure OPEN**.
Application `3d7c88f384b7920744464a7c7570530ca825325a` includes the bounded MainLayout and
browser-state repair from `e6672c14fca3eb2ca22e56771a3bfb2dcedd64e0`.
The [current closure report](workspace-critical-fixes-closure.md) distinguishes this correction from unresolved
[WCL-NAV1](workspace-navigation-acknowledgement-finding.md).

MainLayout fences each asynchronous stage with exact layout, route and profile ownership,
observes queued work, and releases its listener by unique identity. BrowserStateStore
rejects stale profile work and never reports a disconnected Save as acknowledged.
Workbench initializes only after its acknowledged write; active failures remain visible
and retryable. No renderer/JS-dependent teardown drain is introduced.

Three failing layout controls and a failing store control are preserved. The repaired
five layout and three store controls, existing consumers, fresh Stable selection and
deliberately held-query/shared-route browser journey pass. The complete fresh browser
checkpoint nevertheless records an unhandled RemoteNavigationManager cancellation.
Its initiating application call is not known, so isolated passes do not close this shared
application boundary or permit a readiness verdict.

## Historical campaign record

Status: OPEN, priority P2. Application shared-shell readiness is blocked. This finding
does not change Workspace renderer ownership or the existing ConversationShellHost S0 fix.

## Observed behavior

The second full browser sequence (`frozen-browsers-02-closure`) retained a
`JSDisconnectedException` while MainLayout's first-render initialization tracked the
current tab. Collaboration completed its owner assertions, then rejected the shared
host's `Unhandled exception` log. The same log also reproduced WC-C1 and WC-C2; these
are three distinct paths, not one exception attributed to three features.

Entry commit is `1080c24163afd3cf65fcc68756913c5dd3262a62`, WsCompletionProof, private
PostgreSQL 18.6 and unchanged sibling inputs. The exact observed tree is in
`frozen-browsers-02-closure/source.json`; the source map and original log hashes are in
`browser-state-retirement-source-map.json` beneath the ignored campaign root.
`shared-shell-closure-original.log` has SHA-256
`c36b3169687c3a7be4f9cb8e50784108d7e6709cedbe144861d8f6658a3f1a78`.

The stack identifies a circuit already disconnected and being disposed. It does not
retain the original route, browser storage key, snapshot contents or per-entry timestamp.
No wrong-profile write, credential disclosure or lost business mutation was observed.
The final tab snapshot's persistence is unconfirmed; it must not be described as saved.

## Call and ownership map

1. `MainLayout.OnAfterRenderAsync` initializes Workbench, awaits deleted-project cleanup,
   then awaits `ResolveAndTrackCurrentTabAsync` (MainLayout.State.cs, line 134).
2. `ResolveAndTrackCurrentTabAsync` awaits route descriptor resolution, then calls
   `Workbench.TrackTabAsync` (MainLayout.Workbench.cs, line 113).
3. `TrackTabAsync` updates its in-memory tab collection and active identity, then awaits
   `PersistAsync` before publishing its Changed event (WorkbenchTabState.cs, line 272).
4. `PersistAsync` captures `WorkbenchSessionSnapshot` and calls the configured store
   (line 593). `BrowserWorkspaceStateStore.SaveAsync` stamps current profile identity and
   fingerprint, then invokes browser storage at line 61. JS rejects the disconnected
   circuit; the exception reaches RemoteRenderer and CircuitHost.

MainLayout's current synchronous Dispose detaches handlers and disposes its listener
reference. It has a Collaboration-specific retired flag, but no cancellation/retirement
contract covering the complete first-render sequence or queued route tracking. The
observed path passes default cancellation to Workbench. A check before the first await
alone would not protect the later awaits or browser write.

The store is the browser persistence adapter; Workbench owns in-memory tab state and the
shell owns initialization/navigation lifetime. A store-wide catch could make callers
believe a snapshot was persisted. A shell-wide asynchronous drain could deadlock against
renderer or JS retirement. The correct admission, cancellation and observable persistence
outcome require controlled ordering before either change is justified.

## Attribution and repair boundary

The browser-state save originates in `f649011809` (April 1, 2026); the tracked first-render
call originates in `80ac9f5c78` (April 15). The four implicated files are byte-identical
to the observed checkpoint and were not changed by this bundle. Original broad evidence
did not show this path; absence in one sequence is not a historical no-regression proof.
Attribution is **unknown**, with high confidence in the observed call path and lower
confidence in the precise navigation/disposal ordering.

Affected groups are APP-01/APP-15 and GATE-02. APP-10's collection-wide assertion detects
it; isolated feature persistence remains separate evidence. Potential blast radius is all
shell routes, browser tab restoration and database-keyed layout state. No new module
extraction, schema, capability or canonical runtime change is needed to investigate it.

The follow-up shell-lifecycle bundle must hold route resolution and browser persistence
independently, retire the layout/circuit at each await, and assert no new listener/tab
effect occurs after retirement. Preserve active-route saves and report actual persistence
failure predictably; do not pretend a disconnected browser acknowledged the snapshot.
Include current-profile keys, reload/history, two live layouts, queued location changes,
repeated disposal and existing S0 negative cases. Then run BrowserStateStore/Workbench
owner tests, MainLayout tests, rapid real route/circuit navigation and a new complete
browser gate with the original full-log assertions intact.

Containment in this run is explicit failed shared-shell readiness and retained evidence.
Independent private module journeys continue. No blanket exception suppression, retry,
quarantine or broad shell rewrite was applied.
