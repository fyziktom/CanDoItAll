# CanDoItAll.SchedulerPlanner.UI

## Purpose

Complete Scheduler rendering: Calendar, Schedules, New schedule, History, target picker and edit/delete dialogs. The seam accepts editable draft values, view state and explicit intents; no renderer carries StructureAuthority or resolves production services. The Agent affordance is an explicit host slot.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework: `net10.0`
- Authoritative dependencies: [CanDoItAll.SchedulerPlanner.UI.csproj](CanDoItAll.SchedulerPlanner.UI.csproj)

## Validation

```powershell
dotnet build src/UI/CanDoItAll.SchedulerPlanner.UI/CanDoItAll.SchedulerPlanner.UI.csproj --configuration SchedulerUiProof /m:1
```

See the [Scheduler boundary and proof record](../../../docs/architecture/scheduler-ui-boundary.md).

## Assets

The host includes `CanvasLibHeadAssets`, `CanvasLibBodyAssets` with calendar and runtime
assets enabled, BaseLib theme/font assets, its generated `<host>.styles.css` and Blazor.
The runtime asset group supplies Canvas accessibility support even when workbench editors
are not displayed. Each renderer scopes CSS below its own real DOM root. Calendar JS is
`_content/CanDoItAll.SchedulerPlanner.UI/schedulerPlannerCalendarInterop.js`; registration
belongs to one host/key and cannot detach another host's listener.
