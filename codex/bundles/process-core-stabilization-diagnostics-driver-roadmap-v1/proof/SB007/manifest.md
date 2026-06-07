# SB007 Proof Manifest

## Scope
- Subbundle: `SB007 - Route decision diagnostics`
- Objective: add non-breaking route decision diagnostics in Process Core.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Proof
- Focused route parity test: `bundle://proof/SB007/transcripts/route-diagnostics-tests.txt`
- Critical gate integration proof: `bundle://proof/SB009/transcripts/process-dispatch-diagnostics-integration-tests.txt`
- Core API/boundary proof: `bundle://proof/SB009/transcripts/architecture-api-and-boundary-tests.txt`
- Source assertions: `bundle://proof/SB009/transcripts/source-assertions.txt`
- Core dependency scan: `bundle://proof/SB009/transcripts/core-forbidden-token-scan.txt`

## Result
- Existing route decisions are preserved.
- Diagnostics expose reason codes without changing route order.
- No production driver API was introduced.
