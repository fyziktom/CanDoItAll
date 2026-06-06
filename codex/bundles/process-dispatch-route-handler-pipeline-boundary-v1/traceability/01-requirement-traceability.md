# Requirement Traceability

| Requirement | Owning subbundles | Source artifacts | Planned proof |
| --- | --- | --- | --- |
| Preserve all current process automation behavior. | All | `bundle://inputs/00-original-request.md`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs` | Build, focused unit tests, focused integration tests |
| Do not create Process Core. | All gates | `bundle://inputs/02-structured-input.md` | No-core source scan |
| Do not create production driver APIs. | All gates | `bundle://inputs/02-structured-input.md` | No-driver source scan |
| Split claimed dispatch route flow into module-local route handlers. | SB005-SB088 | `bundle://architecture/01-target-solution.md` | Handler-boundary source scans and focused tests |
| Preserve exact route order. | SB010, SB023, SB042, SB063, SB094 | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs` | Route-order architecture test and source scan |
| Keep side effects visible. | SB011, SB012, SB026, SB065-SB068, SB073-SB076 | `bundle://architecture/01-route-handler-boundary.md` | Side-effect ownership scan |
| Avoid UI/mobile/browser proof drift. | All | `bundle://requirements/01-normalized-requirements.md` | No UI/proof drift scan |
| Record individual subbundle proof rows. | SB093, SB105-SB112 | `bundle://reviews/01-execution-report.md` | Completed execution report with SB001-SB112 rows |
