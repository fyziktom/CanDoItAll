# WB2-S0 — complete Gantt cleanup when a pending update faults

## Evidence and severity

Source-derived, bounded component-lifetime defect. Review source [S11] is the actual
Components `GanttChart.razor.cs` at dc573e2b. [S12] is its added interop test family.
No new runtime reproduction was executed during this review. Do not claim a
production incident, data loss, or an authorization bypass on that basis.

The serialization fix is valuable: one interop call runs at a time, an input change
while it waits schedules the newer controlled model, and Dispose waits for a late
create. Keep those guarantees.

## Reproduction to establish before changing code

1. Mount the real GanttChart and let its initial JavaScript create finish.
2. Change its controlled tasks so a JavaScript update starts; hold that update.
3. Start disposing this exact chart while the update still waits.
4. Complete the update with TaskCanceledException/OperationCanceledException (also
   cover a non-JS unexpected fault separately).
5. Observe whether the existing JavaScript canvas receives its own dispose call.

`UpdateInteropAsync` catches JSException only. `DisposeAsync` awaits
`pendingInterop` before calling `DisposeInteropAsync` in the same try. A canceled
or other uncaught fault skips that latter call. The outer finally releases the
DotNetObjectReference, but it does not dispose the already-created JS canvas.
The existing three controlled cases cover delayed successful creation/update and
successful disposal, not this path.

Also reproduce update failure *before* Dispose. Do not confuse a failed initial
create with an existing successful create: track which exact resource was acquired.

## Required behavior, not a prescribed class layout

Cleanup of an acquired resource must not depend on successful completion of the
previous operation. Await/observe the outstanding work, preserve its meaningful
failure, and independently release the exact owned canvas and callback reference.
The same instance must not issue duplicate cleanup; one instance must not affect
another. Expected retired-circuit cancellation may be handled narrowly, but a
live timeout or unrelated error must remain observable. Preserve original errors
when cleanup fails as well. Do not turn any exception into a false success.

Do not remove the wait, revert to parallel interop, copy Gantt into CanDoItAll,
raise global JS timeouts, call a global dispose-all, leak references intentionally,
or swallow every OperationCanceledException without lifetime reasoning.

## Regression matrix

Cover pending update success/cancellation/fault during Dispose; already-faulted
update; delayed create success; failed create without a fabricated existing resource;
JS failure behavior; cleanup failure; repeat Dispose; two live charts; and updates
arriving while create/update waits. Reuse the real controlled IJSRuntime test seam.
Only narrowly needed additions to the original Components test helpers are allowed.

Build/test the actual shared Gantt owner and its affected tests first. Then use the
current WB1 source/published/native chart to exercise close/remount during pending
activity, newest table/canvas agreement and an unaffected second chart. Record the
exact Components SHA, native DLLs and served assets. Source review or a Python model
of the control flow is not runtime proof.

Make this a coherent signed Components checkpoint. Main repo may continue with the
verified local pair; report local-versus-remote delivery honestly. No automatic
push, merge, package release or recreation of the already-published dc573e2b fix.
