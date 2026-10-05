# Calendar boundary

Sources S16–S18. Read the actual ProjectCalendarStateParser and all direct/dynamic callers
before final placement. The legacy embedded wrapper is obsolete; inspect actual reachability
rather than assigning it a new feature. Current route is `/projects/{ProjectId:guid}/calendar`.

## Preserve native semantics

The canonical service assembles owner-contributed nodes, selects those with both persisted
start and end, maps IDs/routes and returns ExpectedProjectAdmission. Do not assemble CRM,
Resources, TestLab or project records in UI. Read setup can involve existing schema/projection
work; avoid accidental per-render rereads but do not invent a zero-write bootstrap guarantee.

Real Calendar is read-only. Keep view/day/week/month/list/year, selected date/event, timezone,
list export and linked-artifact navigation. A typed selected event refers to a record in the
accepted surface; a stale incoming JS EventId does not grant access. Validate missing/unavailable
routes and retain the native safe route policy. UI must not concatenate IDs into arbitrary URLs.

Raw calendar state JSON and its normalized version have one policy. Capture origin before
persisting view state and define same-scope ordering so an older acknowledged write cannot
become the last durable view by accident. Inspect current SaveViewState owner, storage scope
and all callers; preserve its schema and canonical database semantics. If an exact UI writer
needs stronger origin validation, use a narrow native path, not a global profile rewrite.
A view-state update is not an event schedule update or creation command.
The existing view-state storage is keyed by project/surface. Do not silently turn it into a
per-user/per-browser store or promise new cross-circuit conflict semantics. Preserve the native
sharing contract while ordering writes from the same accepted presentation owner.

Project/context load failures get their own retry and availability; no unhandled page failure
or indefinite spinner. A failed refresh can keep only explicitly stale facts for the same
accepted scope, never data from another project. Repeated same-parameter rendering is not
permission for a new load or write. Cancel reads and detach subscriptions on retirement.

The selected detail currently formats server-local time while calendar events expose UTC.
Make the displayed timezone explicit and consistent with the accepted calendar policy. Use
UTC as an explicit fallback only where the existing policy allows it; do not silently replace
stored state. Test in differing host/browser timezone conditions and DST boundaries through
actual shared controls. This does not authorize a new scheduling/timezone engine.

## Demonstrations and assets

Move `BuildValidationSurface` synthetic enrichment and its neutral boundary panels into a
clearly marked sandbox specimen. Production must never display fabricated completed checklist
items or use demonstration AllowEdit flags as a new native capability. Preserve compatibility
contracts if genuine callers require them and document their non-authoritative nature.

Use actual CanvasCalendar and its owned assets, unique active instance/occurrence keys and
existing parser. Do not copy its JavaScript, flatten it into a static grid or test only text.
Both two independent calendars and repeated dispose/reopen must work. A retained legacy
public wrapper can adapt the new renderer without adding an implementation reference to it.

## Required proof

Direct route + back/forward; loading/error/unavailable/empty/selected states; accepted event
IDs and correct internal routes; same-project refresh preserving meaningful selection; A-B-A
project replacement; stale selection/state callbacks; saved view read-back after close/reopen;
view-write ordering; final profile/lifetime refusal; read-only gesture rejection; exact exported
rows; canonical bytes/records unchanged by mere calendar navigation. Context readiness must
match the loaded project, not the desired route. Test Agent completion refresh with the same
original project and with an old notification after navigation.
