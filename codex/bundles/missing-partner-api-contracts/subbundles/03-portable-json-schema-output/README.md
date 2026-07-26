# Portable JSON Schema Output

## Status

- `Completed`

## Objective

- Close N003 with a versioned portable JSON Schema request/result and deterministic
  validation evidence for agent execution.

## Success Criteria

- OpenAPI contains no `.NET Type` in the public execution request.
- Unsupported/oversize/over-complex schemas fail before provider execution.
- Success returns parsed JSON, raw output, schema hash, and valid status.
- refusal, malformed JSON, and schema-invalid output remain distinct.

## Covered Inputs

- N003 / R003.

## Prerequisites

- SB02 closed and agent API architecture checkpoint passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models\OutputContracts\AgentOutputContracts.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models\Conversations\ConversationModels.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Maf\Runtime\MafRuntimeResponseAssembler.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Maf\Runtime\MafRuntimeExecutionOptionsResolver.cs`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration`

## Deliverables

- Public portable structured-output DTO and internal schema contract/adapter.
- Bounded schema canonicalizer/validator and SHA-256 evidence.
- Execution result/evidence fields and explicit failure classifications.
- Provider capability preflight/fallback documentation and tests.

## Dependency Impact

- Critical run-evidence contract consumed by SB06 and response schemas in SB07/SB08.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation.

## Implementation Steps

1. Characterize typed internal contracts and provider capability handling.
2. Add portable DTO and bounded schema validation/canonicalization.
3. Adapt runtime provider call while retaining trusted internal typed contracts.
4. Validate/record raw and parsed output with explicit status.
5. Test portable positive, unsupported, malformed, and schema-invalid cases.

## Scope Exceptions

- Do not remove trusted in-process generic `For<T>` helpers unless required.

## Do Not Do

- Do not accept schema metadata without validating result bytes.
- Do not discard raw provider output or exact canonical schema.
- Do not add fixture-specific branching.

## Acceptance Checklist

- [x] non-.NET JSON request deserializes
- [x] schema limits fail before provider invocation
- [x] schema hash is deterministic
- [x] valid output returns parsed data
- [x] refusal/malformed/schema-invalid are distinguishable

## Proof Required

- Direct schema validator tests.
- Agent execution integration tests with a deterministic fake provider.
- OpenAPI request/response schema assertions.

## Browser Validation Logging

- N/A.

## C# Architecture Impact

### Boundary Ownership

- Web transport adapter; Models portable evidence; Core validator; provider adapter call.

### Dependency Direction

- No JSON/OpenAPI dependency enters provider-independent Models beyond `System.Text.Json`.

### Pattern Decision

- Adapter preserving internal typed contract compatibility.

### Testability Contract

- Validator and adapter tested without full runtime construction.

### Partial Class Policy

- New validator/adapter are top-level types.

### Architecture Proof Required

- Direct tests and source proof that public request no longer exposes `.NET Type`.

## Progression Gate

- Positive/adversarial semantic proof and architecture checkpoint unlock SB04.

## Reopen Triggers

- SB06 cannot reconstruct schema evidence, or OpenAPI reveals Type leakage/runtime drift.
