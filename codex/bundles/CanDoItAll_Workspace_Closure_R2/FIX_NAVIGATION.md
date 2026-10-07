# P2 — WCL-NAV1: navigation acknowledgement and circuit ownership

The prior report identifies a real unhandled TaskCanceledException in
`RemoteNavigationManager.NavigateToCore.PerformNavigationAsync` for `/agents`. It does not
identify the originating application caller with certainty. The 59.999818-second interval
from the preceding usage test is a correlation, not causation. [R04]

The previous WC-C3 changes add useful route/layout/profile fences, exact listener ownership
and truthful browser-save failure handling. Preserve them. The currently observed residual
stack is not the former Simple Chats, Dialog disposal or browser-state-save stack. [R09]

## Reproduction and diagnostic boundaries

Use the same owned Development collection host shape and accumulated route history as the
failed full run, then compare fresh isolated and separately published hosts. Capture UTC
correlation for test/scenario, circuit creation/disconnect/permanent retirement, initiating
view and captured URI, route generation, server navigation start, browser invocation and
actual completion/exception. Do not log browser storage payloads, user messages or credentials.

The existing `AllUsageScopesDriveChartsAndDialogs` changes Chats -> Agents -> Both, checks the
URL and existing chart visibility, then disposes its browser context. Those observations do
not on their own establish the server-side navigation acknowledgement. Confirm whether this
case was actually the initiator before changing its teardown. [R12]

Inspect the exact installed ASP.NET Core version/source, not a guessed current framework.
Instrumentation must be test-owned and bounded; no production NavigationManager replacement.
Hold acknowledgement independently of location visibility. Test these orderings separately:

| Ordering | Required assertion |
| --- | --- |
| Active owner, normal acknowledgement | Exactly one appropriate route/tab update, intended history/back/forward behavior and no false failure. |
| Layout retired before admission | No new navigation, browser save or listener work from that layout. |
| Navigation already admitted, browser retires before acknowledgement | The operation is attributed to its original circuit; expected teardown cannot corrupt a new circuit or hide an unrelated active failure. |
| Active owner, actual JS/framework timeout | Failure remains observable and is not silently reported as a completed navigation or persisted snapshot. |
| A -> B -> A / replaced layout | Delayed descriptor/read/ack from the first A cannot publish into the second A. |
| Two simultaneous contexts | Closing one does not cancel or unregister the other; both profile and listener identities stay correct. |
| Initial startup / refresh / retry | No late first-render registration or new tab from a retired initialization; live startup remains usable. |

A fixed one-minute wait at every test end is not an adequate ownership contract. A bounded
late-log observation is useful diagnostic coverage after exact operations are correlated,
not a substitute for synchronization. Do not lower connection retention or alter JS timeout
settings to make the failure disappear.

## Selecting the repair

If the app dispatches from a retired owner, correct admission at that owner and propagate
its precise lifetime through the asynchronous stages. If the test closes a valid circuit
before a required acknowledged operation, wait for a genuine completion signal and preserve
a negative abrupt-disconnect test. If framework lifecycle behavior is demonstrated, retain
a minimal reproducer and choose a compatible bounded remedy supported by actual versioned
source. Do not upgrade runtime packages opportunistically or create a global catch/allowlist.

The accepted report must say whether this is a product fix, a test observation correction,
or an unresolved framework/environment limitation. It must not attribute all previous
shared-host exceptions to the last module simply because that test reads the complete log.

## Closure proof

Run the existing MainLayout, BrowserStateStore, Workbench navigation/history and shared
lifetime controls; add failing-first exact-ack tests. Use real nested dialogs, usage scope
changes, Settings sections, Project Structure and Simple Chats in shared host history. Both
normal and abrupt retirement must be covered. Inspect whole process logs and circuit events,
not just each page's console. Repeat the fresh full browser inventory on the final source
pair; isolated successful runs remain scoped evidence, never retroactive repair of the old
broad failure. Preserve all active-error and no-wrong-profile assertions.
