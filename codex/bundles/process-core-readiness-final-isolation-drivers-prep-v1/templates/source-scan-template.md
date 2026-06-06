# Source Scan Template

Required scans:

```bash
rg -n "CanDoItAll\.Processes\.Core|CanDoItAll\.Modules\.Processes\.Core|IProcessDriverPack|IProcessDriverRegistry|ProcessDriverRegistry|DriverPack" src
rg -n "TODO|NotImplementedException|throw new NotImplementedException|return default" src/CanDoItAll.Modules.Processes/Automation/Dispatch
git diff --name-only -- . ':!codex/bundles' | rg '\.(razor|css|js|ts|tsx|scss|png|jpg|jpeg|webp|gif|svg)$|mobile|small|medium|phone|tablet'
```
