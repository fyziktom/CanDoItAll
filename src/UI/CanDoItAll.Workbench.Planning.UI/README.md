# Workbench planning rendering

This light Razor library owns the Calendar and complete Gantt presentation wired
to their native Workbench hosts. Both task form families follow in WB1.

`PlanningCalendarSurface` renders the actual neutral `CanvasCalendar`, availability,
selected-event details and view controls. `CalendarPresentation` contains rendered
facts and an occurrence identity. Its typed intents preserve the original origin
and callback. They confer no project admission or native write capability.

Canonical reads, accepted event resolution, internal navigation, Agent context and
ordered view writes remain in `ProjectCalendarPage`. The parser keeps its existing
public namespace and has a type forward in the native module. View JSON preserves
unknown fields while applying the normalized known state. Explicit timezone
formatting handles daylight-saving offsets without server-local conversion.

`PlanningGanttSurface` mounts the actual Gantt chart, all seven gesture callbacks,
dependency removal, task drag source, summaries and strict Mermaid preview. Its
typed intents retain the original receiver and occurrence. The native host owns
projection, mutation admission, expected schedules, row persistence and recovery.
Mermaid source, copy and downloads describe one frozen accepted projection; the
neutral chart owns PNG encoding. A renderer never constructs project authority.

Dependencies are framework components and neutral BaseLib, CanvasLib, Gantt and Mermaid.
The independent [sandbox](../../Sandboxes/CanDoItAll.Workbench.Planning.UiSandbox/README.md)
provides synthetic controls without an application database. Native fault tests
complement the isolated tests under `tests/Components/CanDoItAll.Workbench.Planning.UI.Tests`.
