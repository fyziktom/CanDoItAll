# SB10 Semantic Invariants

## SB10-INV-001 Provider-Native Browser Write Migration

- Invariant ID: SB10-INV-001
- Source raw note: RQ-009 requires provider-native browser writes to use the coordinator without collapsing expected and discovered modes.
- Expected behavior: Expected provider-native outputs use PlanExpectedOutput, discovered outputs use PlanDiscoveredOutput, and both record through WriteAsync after dispatcher-owned path safety and file copy.
- Disallowed shallow implementation: A shallow pass would merge expected/discovered planning or move browser output discovery and file-copy logic into the coordinator.
- Failing-first test: bundle://proof/SB10/transcripts/failing-first-provider-native-browser-source-guard.txt
- Passing test: bundle://proof/SB10/transcripts/provider-native-browser-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs; repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: bundle://proof/SB10/source-assertions/provider-native-browser-source-scan.txt
- Red-team negative case: The failing-first source guard captured missing coordinator usage in provider-native expected/discovered sections.
- Downstream dependency check: SB11 and SB12 depend on provider-native modes remaining source-adapter-owned before final smoke.

