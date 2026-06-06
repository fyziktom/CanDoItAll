# Line Count And Source Scan Proof

Captured after implementation.

## Line Counts

| Lines | File |
| ---: | --- |
| 208 | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` |
| 88 | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs` |
| 319 | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs` |
| 206 | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs` |
| 227 | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs` |
| 97 | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs` |
| 40 | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStepTransitionService.cs` |
| 31 | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRunClosureGuardService.cs` |

## Hard Constraint Scans

- `rg CanDoItAll\.Processes\.Core src/CanDoItAll.Modules.Processes`: pass.
- `rg CanDoItAll\.Modules\.Processes\.Core src/CanDoItAll.Modules.Processes`: pass.
- `rg IProcessDriverPack src/CanDoItAll.Modules.Processes`: pass.
- `rg IProcessDriverRegistry src/CanDoItAll.Modules.Processes`: pass.
- `rg ProcessDriverRegistry src/CanDoItAll.Modules.Processes`: pass.
- `rg ProcessDriver src/CanDoItAll.Modules.Processes`: pass.
- `rg DriverPack src/CanDoItAll.Modules.Processes`: pass.
- `git diff --name-only -- . ':!codex/bundles' | rg '\.(razor|css|js|ts|tsx|scss|png|jpg|jpeg|webp|gif|svg)$'`: pass, no UI/media files changed outside bundle metadata.

## Build And Test Proof

- `dotnet build CanDoItAll.slnx --no-restore`: pass, 0 warnings, 0 errors.
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore`: pass, 1005 passed.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter FullyQualifiedName~ProcessRunAutomationDispatchServiceTests`: pass, 528 passed.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessSubprocessIntegrationTests|FullyQualifiedName~ProcessArtifactProjectionWriteCoordinatorTests|FullyQualifiedName~ProcessAutomationExecutionClientTests"`: pass, 14 passed.
- Full unfiltered integration project run was attempted, exceeded the command window after more than ten minutes, and the orphaned test process tree was stopped. Focused integration coverage above is the closure proof for moved dispatch paths.
