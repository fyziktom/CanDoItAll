# SB015 Semantic Invariants

- Invariant ID: `SB015-SUBPROCESS-STAYS-MODULE-LOCAL`
- Source raw note: Broader Core extraction remains blocked by subprocess lifecycle and application-service coupling.
- Expected behavior: Subprocess dispatch, artifact resolution, transitions, and persistence remain in the Processes module.
- Disallowed shallow implementation: Moving subprocess lifecycle services into Core or exposing driver-ready subprocess runtime APIs.
- Failing-first test: N/A process/no production behavior; this subbundle verifies non-movement through guard tests and source scans.
- Passing test: bundle://proof/common/transcripts/unit-architecture.txt and bundle://proof/common/transcripts/integration-dispatch.txt
- Changed source files: repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs and repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: Core contains only `IsSubprocess` and route decision logic, not subprocess execution.
- Red-team negative case: bundle://proof/common/transcripts/production-driver-token-scan.txt rejects helper-driver and registry tokens.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves downstream subprocess consumers still compile.
