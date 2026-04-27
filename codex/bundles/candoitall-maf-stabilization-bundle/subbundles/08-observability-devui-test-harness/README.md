# 08 - Observability, Tracing, and Agent Test Harness

## Objective

Make agent behavior diagnosable and regression-testable. Existing logging/OpenTelemetry exists, but validation, repair, finalizer, tool policy, and structured-output state need consistent traces and test harness coverage.

## Primary files to inspect


- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs`
- Existing test projects or create appropriate test fixtures.
- Existing docs and scenario harness code.


## Required implementation tasks


1. Define a consistent observability schema for agent runs:
   - agent id
   - process id
   - step id
   - execution run id
   - correlation id
   - provider/model
   - session mode
   - structured-output contract key
   - finalizer policy
   - tool policy decisions
   - pending approvals
   - validation errors
   - repair attempt count
   - raw output hash
   - final outcome
2. Add OpenTelemetry tags/spans where appropriate.
3. Ensure logs redact secrets and sensitive payloads.
4. Add or update a deterministic test harness for MAF runtime behavior with fake/mock agents/providers where feasible.
5. Add optional live-provider tests behind settings/environment guards; they must be skipped by default when credentials are absent.
6. Add a developer-facing guide for interpreting traces.


## Required tests


Unit tests:
- Observability events include required IDs and omit secrets.
- Validation errors are captured with code/path/message.
- Repair attempts are counted.
- Tool policy decisions are logged/traced.
- Finalizer status is logged/traced.

Integration tests:
- Deterministic process mock/calculator flow emits the expected trace markers.
- Invalid structured output emits validation failure trace.
- Tool policy denial emits policy trace.


## Risks and constraints


- Over-logging can leak secrets or make traces noisy. Redaction and sampling must be considered.

