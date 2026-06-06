# Proof Requirements

Required proof at final closure:

- `dotnet build CanDoItAll.slnx --no-restore`
- Focused unit tests for route order, claim lifecycle, exception closure and handler boundaries
- Focused integration tests for subprocess, workflow, direct-agent dispatch and failure closure
- Source scan proving:
  - no `CanDoItAll.Processes.Core`
  - no `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `IProcessHelperDriver`
  - no UI/Razor/CSS/JS/TS/image/screenshot/mobile proof drift
  - no TODO/NotImplemented/stub markers in changed production dispatch files
  - explicit route handler order
- Line-count review for Dispatch.cs and RouteExecution.cs.
- Known unrelated failures documented separately.
