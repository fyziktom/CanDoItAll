# 06 Generic Memory Module Persistence And Services

## Status

- `Completed`

## Objective

- Implement generic memory module persistence and core services for provider profiles, operations, feedback, events, source requests, policies, and status projection.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R02
- R04
- R05
- R06
- R19

## Prerequisites

- SB05 gate passed

## Exact Source References

- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderDispatchModels.cs`
- `bundle://architecture/01-target-solution.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Create generic memory application/persistence services for provider profiles, provider registry, operation ledger, feedback ledger, event inbox/outbox, source request ledger, and policy projection.
- Register EF configurations for generic integration metadata in the main app persistence layer without adding native memory domain records.
- Add `IDbContextFactory<AppDbContext>`-based async service implementations using no-tracking reads where mutation is not required.
- Add a deterministic mock driver/provider runtime for tests and UI demos.
- Add service collection extensions and options validation for generic memory module registration.
- Register the generic memory module so base services, provider management, and diagnostics work with zero configured providers.
- Do not register any mock, native, OpenAI, Qdrant, or in-process provider unless configuration explicitly enables that provider profile.

## Dependency Impact

- Drivers, UI, MAF tools, and feedback depend on the generic runtime services.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create the planned generic memory projects or folders according to `architecture/01-target-solution.md` and current solution conventions.
2. Add EF records/configurations for generic metadata only; keep native entities out of the main AppDbContext after this point except old migration compatibility paths.
3. Implement application services for provider lookup, operation creation/status, feedback submission, event enqueue/dequeue, and source request enqueue.
4. Add async tests proving no-tracking query behavior, status updates, retention cleanup query shape, and provider profile persistence.
5. Wire module registration without enabling any provider by default.
6. Add tests proving an operation request with no configured provider produces a typed no-provider state and no driver dispatch.

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
- Generic memory module registration succeeds with zero providers and no Qdrant configuration.
- Generic integration metadata persists independently from native memory domain records.
- All persistence services use async APIs and avoid long-lived DbContext instances.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB06/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB06/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB06/manifest.md` and `proof/SB06/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run unit/integration tests for provider profile persistence, operation ledger persistence, feedback ledger persistence, and event inbox/outbox persistence.
- Run source audit proving no native memory domain entities were added to new generic persistence records.
- Run service registration tests with no provider configuration, no native memory module, and no Qdrant configuration.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB06 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB06 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
