# 07 Http Driver And Resilience Policies

## Status

- `Completed`

## Objective

- Add HTTP memory provider driver with typed request mapping, auth policy, timeout, retry/cancellation behavior, health check, and simple sync/async support.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R03
- R04
- R17

## Prerequisites

- SB06 completed

## Exact Source References

- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Projection/CognitiveMemoryProjectionAdapters.cs`
- `bundle://architecture/02-protocol-contract-model.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Implement HTTP provider driver using typed `HttpClient`/factory registration, provider profile options, authentication header policy, timeout budget, cancellation token propagation, and health checks.
- Map generic envelopes to HTTP request/response bodies while preserving operation id, correlation id, capability id, and protocol version.
- Support sync `MemoryContextPack`, async `MemoryOperationAccepted`, provider error, timeout, unavailable provider, and unsupported capability responses.
- Add resiliency options without hiding provider failures or retrying non-idempotent operations incorrectly.
- Add a test HTTP provider fixture for sync, async, delayed, timeout, malformed response, and health-degraded scenarios.

## Dependency Impact

- Simple providers and native remote provider driver patterns depend on correct HTTP transport semantics.

## Validation Depth

- `Driver contract`

## Implementation Steps

1. Add the HTTP driver package/project and DI registration behind the generic driver factory.
2. Define HTTP endpoint conventions for query, ingest, feedback, status, events, and health, but keep native-specific endpoints out.
3. Implement response mapping and error classification into generic operation status records.
4. Add tests that verify cancellation token propagation and timeout budgets are applied per operation.
5. Document the minimum contract a plain external HTTP memory provider must implement.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- A simple HTTP provider can receive a plain query-like payload while the host ledger retains the full structured envelope.
- Timeouts, cancellations, malformed responses, and provider errors become observable operation states rather than blocking agent execution.
- HTTP driver registration does not require Qdrant, native memory, or app module internals.

## Proof Required

- Create `proof/SB07/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run HTTP driver tests for sync result, async accepted result, timeout, cancellation, health failure, malformed response, and unsupported capability.
- Run dependency audit proving the HTTP driver depends only on generic abstractions/application contracts plus HTTP infrastructure.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB07 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB07 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
