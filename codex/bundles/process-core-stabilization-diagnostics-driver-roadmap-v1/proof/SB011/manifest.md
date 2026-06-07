# SB011 Proof Manifest

## Scope
- Subbundle: `SB011 - Finalizer/direct-agent adapter edge hardening`
- Objective: tighten adapter ownership for finalizer/direct-agent compatibility.

## Changed Sources
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Proof
- Focused finalizer/direct-agent edge test: `bundle://proof/SB011/transcripts/finalizer-direct-agent-adapter-edge-test.txt`
- Invalid route-claim negative test: `bundle://proof/SB011/transcripts/finalizer-invalid-route-claim-negative-test.txt`
- Critical gate integration proof: `bundle://proof/SB012/transcripts/process-dispatch-adapter-integration-tests.txt`
- Source assertions: `bundle://proof/SB012/transcripts/source-assertions.txt`

## Result
- Finalizer application service remains DTO-facing and does not use dispatcher payload aliases.
- Direct-agent execution conversion remains isolated in `ProcessDispatchDirectAgentExecutionAdapter`.
- Unadapted route dispatch claims fail predictably instead of being silently converted.
