# Source Scan Commands

Run at every critical gate.

```powershell
rg -n "CanDoItAll\.Processes\.Core|CanDoItAll\.Modules\.Processes\.Core|IProcessDriverPack|IProcessDriverRegistry|ProcessDriverRegistry|DriverPack|ProcessDriver" src tests
rg -n "TODO|NotImplementedException|throw new NotImplementedException|placeholder|stub" src/CanDoItAll.Modules.Processes/Automation/Dispatch tests/CanDoItAll.Tests.Unit tests/CanDoItAll.Tests.Integration
git diff --name-only -- . ':!codex/bundles' | rg "\.(razor|css|js|ts|tsx|scss|png|jpg|jpeg|webp|gif|svg)$|mobile|small-screen|medium-screen|phone|tablet"
```

Route/finalizer/projection-specific scans must be added by subbundle implementation.
