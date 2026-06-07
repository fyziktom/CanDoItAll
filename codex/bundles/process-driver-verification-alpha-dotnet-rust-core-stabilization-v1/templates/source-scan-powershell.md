# Source Scan Template

```powershell
rg -n "ProcessDriverRegistry|DriverRegistry|DriverSelector|RuntimeDriver|AddProcessDriver|IServiceCollection|Process\.Start|GraphServiceClient|TransitionStep|DispatchClaim|FinalizeDirectAgent|ScheduleRetry|Workspace|Storage|DbContext|AppDbContext" src/CanDoItAll.Processes.Drivers.TranscriptVerification src/CanDoItAll.Processes.Drivers.Abstractions src/CanDoItAll.Processes.Core
git diff --name-only -- . ':!codex/bundles' | rg '\.(razor|css|js|ts|tsx|scss|png|jpg|jpeg|webp|gif|svg)$'
```
