# Workbench planning sandbox

This standalone host renders the actual planning controls with explicit synthetic
data. It has no native module, database, application bootstrap or credential store.
Calendar provides availability scenarios, two independent instances and the
former production boundary specimens. Gantt mounts the same chart, drag source,
gesture intents and export preview with independent synthetic task graphs. Task
forms and the complete outcome scenarios are completed in WB1's editor stage.

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
