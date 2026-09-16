# 11. UI seams, composition, and sequencing

## Current branch decision

Finish the **currently bounded Agents decoupling** at a stable checkpoint. This does not require finishing every future voice/chat/Simple Chats roadmap item. Record exact scope, commit, discovery, and affected behavior.

Then do not start broad UI extraction across all other modules. First establish correct authority, application contracts, and safe cross-module operations for the selected area; resume its UI extraction once that interface is stable. Do not wait for every DbContext in the product to be split. Presentation fixes, CSS/JS isolation, accessibility within current scope, state lifetime, and regression fixes may continue if they do not freeze incorrect data ownership.

## Responsibility split

Reusable components render view state and emit intent. Scoped application adapters load/command, map pending operations, and expose immutable results. Domain owners validate/transact/publish effects. Shell owns windows/tabs/navigation/focus and adapter-instance binding.

Neutral conversation presentation has no Agents runtime, Process state machine, provider secrets, transcript persistence, or approval policy. Agents adapter supplies agent context/tools/approval state; Simple Chats adapter supplies ordinary state. Equal appearance does not imply equal stores or execution.

Razor lifetime is not long-running execution ownership. Dispose subscriptions/JS handles and fence stale callbacks. Cancelling a data load is not cancelling an admitted operation. Characterize explicit legacy session-bound behavior rather than silently changing it.

## View versus domain state

Coordinates, selection, expansion, viewport, row order, dialog tabs, schedules, and dependencies are different facts. Scheduling/assignments are Work Management, not canvas JSON. Unsaved drafts stay local until confirmed. Do not erase conflict/unconfirmed state and user work with blind reload. Read-your-writes accepts owner revision; older projection cannot overwrite it.

Every load has session/scope generation and cancellation. Late results are ignored for the new view without claiming an original durable operation was rolled back. Preserve actual existing deep links and back/forward state; do not claim all dialogs become newly bookmarkable. Keep current PC/large-screen focus, not a mobile redesign.

## Composition and availability

Composition may reference all implementations but owns no business facts. Use explicit feature registrations/adapters, not a new universal plugin system. Preserve existing dynamic plugin grants/registry. A standalone fake UI host is not production-integration proof.

Missing required writer/security/executor fails closed; optional reader absence is explicit. Verify single-service and IEnumerable resolution. AddScoped(real) plus TryAddScoped(fallback) is not the same order problem as two competing AddScoped registrations; inspect actual usage rather than repeat an inherited label.

## Build and hot reload

Measure real transitive presentation dependencies. Moving runtime into Runtime.Hosting while UI still imports it is not isolation. No success metric based only on removed using statements or project counts. Preserve behavior while shrinking the graph. Change bootstrap/development host only as needed for the affected seam, not as a CI redesign.
