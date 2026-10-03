# S0 — bounded Tooltip lifecycle cancellation follow-up

## Evidence and exact scope

The PP2 closure report records two Tooltip disposal cancellations around detached failed-attempt
circuits, outside successful file/image intervals. Current `TooltipInterop` treats only
`JSDisconnectedException` and `ObjectDisposedException` as lifecycle exceptions. `DisposeModuleAsync`
awaits `IJSObjectReference.DisposeAsync`; cancellation is not in that filter. Imports and normal calls
use the same filter and serialize through `operationGate`. Existing tests cover one shared import
and a successful delayed import during disposal. [S05,S12,S13]

This establishes a concrete unhandled source path consistent with the report, not the exact
identity of a browser request in unavailable private logs. Obtain the original safe stack/circuit
coordinates when available, then make a deterministic reproduction independent of them.

## First failing tests

1. Imported module exists; its DisposeAsync completes with TaskCanceledException. Dispose the real
   TooltipInterop/Tooltip owner. Verify how the fault currently escapes and that cleanup is needed.
2. Begin import, retire owner, then complete import successfully; the module must be released once,
   with no anchor/clamp/clear invocation on a successor. Repeat with canceled import and failed import.
3. Hold anchor/clear/clamp interop, queue another operation and retire the component. Release each
   branch deterministically; no stale callback, lost gate release, double module disposal or deadlock.
4. Dispose twice/concurrently; invoke after disposal. Exercise two Tooltip/TooltipTarget owners so
   cleanup of A never removes B's listener/focus behavior. Keep old passing tests.
5. Negative controls: ordinary active-view JSException or unrelated programming failure must remain
   diagnosable. Expected owner/circuit teardown is not a license to catch all OperationCanceledException
   everywhere. Any chosen cancellation filter must be tied to the actual lifecycle/owned resource.

Use barriers/completion sources rather than timing sleeps. Test current cancellation, delayed
ignored cancellation and cleanup failure separately. Tests must prove real resource ownership, not
only that the log stopped printing a message. Do not change global JS or circuit timeouts.

## Repair and delivery

Patch the smallest coherent Tooltip family in `CanDoItAll.Components` on the supplied current branch.
No copied tooltip in the application, blanket JS-runtime wrapper, static singleton or new modal
framework. Preserve the previous Dialog fix. Do not dispose a synchronization primitive while its
accepted users can still Release it. Do not introduce an unbounded synchronous wait on the renderer.
A genuine unrelated failure may still propagate after remaining cleanup, with safe diagnostics.

Rebuild relevant BaseLib tests and the application against the exact signed sibling commit. Run
ordinary hover/focus behavior, close of nested History dialogs and removal/navigation of provider
controls in a real Blazor host. Retain a negative control for an active failing interop request.
Package/asset/source substitution must be documented; a changed local checkout is not proof that a
published package or CI branch resolves it. Follow [dependency delivery](DEPENDENCIES_AND_DELIVERY.md).

This is a bounded prerequisite, not a new PP2 closure loop. Once its controlled regression and
consumer checks pass, continue PP3. If the SDK/sibling already contains an equivalent fix, prove it
and do not create a redundant patch. [S12,S13]
