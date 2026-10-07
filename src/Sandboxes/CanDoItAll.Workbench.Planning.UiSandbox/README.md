# Workbench planning sandbox

This standalone host renders the actual planning controls with explicit synthetic
data. It has no native module, database, application bootstrap or credential store.
Calendar provides availability scenarios, two independent instances and the
former production boundary specimens. Gantt mounts the same chart, drag source,
gesture intents and export preview with independent synthetic task graphs. Both
real task form families support create/edit, raw invalid input, restricted direct
assignment, held quotes, partial/unknown save results and readback without replay.
Stacked forms have independent openings. Gantt Add and both double-click entry
points use those same forms and update only their own in-memory scenario.

From the repository root:

```powershell
npm run planning:css:build
dotnet run --project src/Sandboxes/CanDoItAll.Workbench.Planning.UiSandbox -p:PlanningAssetMode=Fast --no-launch-profile
```

Use `PlanningAssetMode=Parity` after `npm run tailwind:build` for the existing
application theme. Both modes serve neutral component assets from their original
owners. Publish with the same property to obtain an independent deployment; the
runtime mode cannot silently substitute another build's theme. Validation uses
1920 by 1080 at scale 1. Synthetic editor flags never enable native Calendar CRUD.
