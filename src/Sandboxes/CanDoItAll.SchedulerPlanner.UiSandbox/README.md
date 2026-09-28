# CanDoItAll.SchedulerPlanner.UiSandbox

## Purpose

Standalone representative Scheduler workspace using the shipped renderers and presentation policy. Bounded local storage, fixed clock, controllable waits and harmless Agent intent replace only the owner boundary. No EF, Quartz host, database, Workflow execution or Agent runtime starts here.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework: `net10.0`
- Authoritative dependencies: [CanDoItAll.SchedulerPlanner.UiSandbox.csproj](CanDoItAll.SchedulerPlanner.UiSandbox.csproj)

## Validation

```powershell
dotnet build src/Sandboxes/CanDoItAll.SchedulerPlanner.UiSandbox/CanDoItAll.SchedulerPlanner.UiSandbox.csproj --configuration SchedulerUiProof /m:1
```

See the [Scheduler boundary and proof record](../../../docs/architecture/scheduler-ui-boundary.md).

## Assets

The host includes `CanvasLibHeadAssets`, `CanvasLibBodyAssets` with calendar and runtime
assets enabled, BaseLib theme/font assets, its generated `<host>.styles.css` and Blazor.
The runtime asset group supplies Canvas accessibility support even when workbench editors
are not displayed. Each renderer scopes CSS below its own real DOM root. Calendar JS is
`_content/CanDoItAll.SchedulerPlanner.UI/schedulerPlannerCalendarInterop.js`; registration
belongs to one host/key and cannot detach another host's listener.

Run `dotnet run --project src/Sandboxes/CanDoItAll.SchedulerPlanner.UiSandbox --configuration SchedulerUiProof --no-launch-profile -- --urls http://127.0.0.1:59137`
with a task-owned free port. The project explicitly links the authoritative Web `output.css`
as content, without a Web project reference. Rebuild it with `npm ci --prefix Tailwind`
and `npm run tailwind:build` from the repository root when theme inputs change. Missing
CSS fails the build. Publish normally with `dotnet publish`; generated static asset URLs
and the host scoped stylesheet are required in Production too.

The scenario selector includes representative, empty, bounded large, missing version/option,
malformed JSON, read failure, persisted projection/readback failures, unknown/refused save
and Agent readiness. Hold next selects workspace/default/editor/schema/options/validation/
authority/save/readback. Release completes controlled waits. Scenario reset disposes the old
workspace and releases its waits against the old store; admitted writes never migrate to a
successor store. Two independently mounted surfaces exercise calendar registration ownership.
CRON descriptions and projected events are labeled scenario data. They are not Quartz proof.
