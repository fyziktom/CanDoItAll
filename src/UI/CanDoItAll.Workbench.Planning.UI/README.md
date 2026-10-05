# Workbench planning rendering

This light Razor library owns the project planning presentation. Calendar is wired
to the native Workbench route; Gantt and both task form families follow in WB1.

`PlanningCalendarSurface` renders the actual neutral `CanvasCalendar`, availability,
selected-event details and view controls. `CalendarPresentation` contains rendered
facts and an occurrence identity. Its typed intents preserve the original origin
and callback. They confer no project admission or native write capability.

Canonical reads, accepted event resolution, internal navigation, Agent context and
ordered view writes remain in `ProjectCalendarPage`. The parser keeps its existing
public namespace and has a type forward in the native module. View JSON preserves
unknown fields while applying the normalized known state. Explicit timezone
formatting handles daylight-saving offsets without server-local conversion.

Dependencies are framework components and neutral Components BaseLib/CanvasLib.
The independent [sandbox](../../Sandboxes/CanDoItAll.Workbench.Planning.UiSandbox/README.md)
provides synthetic controls without an application database. Native fault tests
complement the isolated tests under `tests/Components/CanDoItAll.Workbench.Planning.UI.Tests`.
