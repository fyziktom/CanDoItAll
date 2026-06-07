# SB022 Proof Manifest

## Scope
- Subbundle: `SB022 - Core consumer allowed-call-site map`
- Objective: document and enforce where `CanDoItAll.Modules.Processes` may consume `CanDoItAll.Processes.Core`.

## Changed Sources
- `repo://codex/bundles/process-core-stabilization-diagnostics-driver-roadmap-v1/architecture/05-core-consumer-allowed-call-site-map.md`
- `repo://src/CanDoItAll.Modules.Processes/GlobalUsings.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHeaderSelector.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteExecutionModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlerPipeline.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRunClosureGuardService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Proof
- Focused call-site map test: `bundle://proof/SB022/transcripts/core-consumer-allowed-call-site-map-test.txt`
- Critical gate architecture proof: `bundle://proof/SB024/transcripts/architecture-core-consumer-boundary-tests.txt`
- Source assertions: `bundle://proof/SB024/transcripts/source-assertions.txt`
- Changed-file hashes: `bundle://proof/SB024/transcripts/changed-file-hashes.txt`

## Result
- The project-wide Process Core global using was removed.
- Process Core consumers in the dispatch layer are explicit and covered by an exact allow-list.
- The architecture map rejects wildcard/directory-wide exemptions.
