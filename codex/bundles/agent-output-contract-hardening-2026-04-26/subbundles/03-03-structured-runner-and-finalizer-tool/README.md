# 03-structured-runner-and-finalizer-tool

## Status

- `Completed`

## Objective

Wire Microsoft Agent Framework execution through typed structured output configuration and add the central runner/finalizer-tool support needed for critical decisions.

## Covered Inputs

- Required concepts 2, 3, 5, 6, 8, and 10.
- Integration tests for structured output configuration, repair/failure, and finalizer-tool enforcement where feasible.
- Bundle requirements R2, R3, R5, R6, R8, R10, and R12.

## Prerequisites

- Subbundle 01 audit complete.
- Subbundle 02 contracts and validators available.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.AgentFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.Session.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProcessTools.cs`

## Deliverables

- Runtime request plumbing for a typed structured output contract.
- Microsoft Agent Framework `ChatOptions.ResponseFormat` configuration using `ChatResponseFormat.ForJsonSchema<T>()` or equivalent installed API.
- Central typed runner or execution helper that captures raw output, deserializes, validates, retries/repairs within limits, and returns typed failure on exhaustion.
- Finalizer-tool pattern support for critical decisions, with typed tool registration using `AIFunctionFactory.Create(...)` where implemented.
- Prompt updates aligned with technical enforcement.

## Dependency Impact

- Process-state integration depends on the runtime actually asking the provider for schema-constrained object output. Without this, process dispatch would still be validating unconstrained text after the fact.

## Validation Depth

- Process-critical closure.

## Implementation Steps

1. Extend execution request/runtime contracts with an optional structured output descriptor.
2. Configure `ChatOptions.ResponseFormat` when the descriptor is present.
3. Add typed runner/repair services at the core boundary.
4. Add finalizer-tool capture and validation support for critical decision agents where the current design permits.
5. Add focused tests for option wiring, retry limits, and failure behavior.

## Scope Exceptions

- Provider support may vary. The implementation must document framework/provider limitations instead of pretending all providers enforce schemas equally.
- Existing non-critical free-form chat paths may remain free-form when they do not drive workflow state.

## Do Not Do

- Do not accept prompt-only JSON as a substitute for `ResponseFormat`.
- Do not use top-level arrays or primitives for structured output.
- Do not silently accept invalid repaired output.
- Do not log raw sensitive data without redaction or hashing.

## Acceptance Checklist

- Structured output descriptor reaches the MAF `ChatOptions`.
- Retry count is bounded and configurable.
- Invalid output returns typed failure/escalation instead of success.
- Finalizer-tool paths fail when the required call is missing.
- Tests or deterministic fakes prove the runner behavior without requiring live model calls.

## Proof Required

- Targeted test command proving runtime option wiring and runner behavior.
- Build evidence after runtime contract changes.

## Browser Validation Logging

- N/A.

## Progression Gate

- Subbundle 04 may proceed only after process dispatch can request structured output and receive a validated typed result or typed failure.

## Suggested Agent Prompt

```text
Implement only subbundle 03. Wire structured output and typed runner behavior with tests, using installed Microsoft Agent Framework APIs.
```
