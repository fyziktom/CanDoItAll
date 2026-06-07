# SB015 Semantic Invariants

## Invariants

- Invariant ID: `SB015-INV-001`
- Source raw note: `Preserve pre-execution block transition, no-op, materialization request, fingerprint/dedup, and start reload behavior while keeping Core deferred.`
- Expected behavior: Database requirement handling blocks only through valid target transitions and no-ops unsupported targets; missing upstream materialization selects runnable missing inputs, deduplicates by stable fingerprint, builds scoped rerun directives, and keeps journal/rerun side effects application-local; start-transition handling reloads and continues refreshed candidates correctly.
- Disallowed shallow implementation: A refactor that keeps method names but changes block targets, removes no-op protection, duplicates materialization requests, broadens rerun directives, or loses refreshed route candidates.
- Failing-first test: `N/A - no production behavior change was intended; this critical gate validates SB013/SB014 behavior-preserving pre-execution refactors.`
- Passing test: `bundle://proof/SB015/transcripts/pre-execution-parity-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionRouteFacts.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDatabaseRequirementBlocker.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterializationSideEffects.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB015/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Changing database target status, making fingerprint order-sensitive, removing target sensitivity, broadening rerun directive duplicate artifact titles, or failing to update route context on start reload fails Gate E tests.
- Downstream dependency check: `SB016` may start subprocess runtime input stabilization because pre-execution parity is proved.

## Raw Note Closure

- Preserve pre-execution behavior: `Solved for Gate E; later gates own subprocess, execution, projection, artifact, wrapper, Core rehearsal, and driver readiness parity.`
- Do not rush Process Core: `Partially solved by explicit pre-execution owners without creating Core; final decision remains owned by SB036.`
- No production driver API: `Partially solved by Gate E source scans; final driver decision remains owned by SB033/SB036.`
