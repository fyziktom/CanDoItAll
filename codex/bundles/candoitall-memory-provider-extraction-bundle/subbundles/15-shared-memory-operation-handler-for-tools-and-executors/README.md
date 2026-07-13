# 15 Shared Memory Operation Handler For Tools And Executors

## Status

- `Completed`

## Objective

- Implement shared memory operation handler used by tool calls, workflow executors, context contributors, UI operations, and API endpoints.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R09
- R04
- R06

## Prerequisites

- SB14 gate passed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentContextContributionContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs`
- `bundle://architecture/04-runtime-operations-and-feedback.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Implement a shared memory operation handler used by tools, workflow executors, context contributors, UI actions, API endpoints, and source ingestion actions.
- Centralize provider selection, capability validation, policy enforcement, operation ledger creation, source snapshot attachment, driver dispatch, async accepted handling, and feedback handle creation.
- Define request builders for query, ingestion, feedback, status, cancellation, event acknowledge, and source request handling.
- Add a thin compatibility adapter for existing native in-process memory only if needed for strangler migration, clearly marked temporary.
- Add tests proving tool and workflow executor routes call the same handler and produce equivalent operation records.
- Make the handler the only shared path for tools, workflow executors, context contributors, UI actions, APIs, provider source requests, and manual ingestion.
- Implement no-provider and capability-mismatch results in the handler so every caller receives the same typed denial instead of selecting a hidden fallback provider.

## Dependency Impact

- All MAF and UI memory invocations must route through this handler to avoid duplicate behavior.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create operation handler application service and request/response boundary models.
2. Refactor any existing MAF memory calls to depend on the handler abstraction instead of direct native recall/probe services.
3. Implement provider selection and capability checks before dispatch.
4. Wire operation ledger and feedback delivery ids into handler responses.
5. Add tests that invoke the handler through fake tool, fake executor, UI-like request, and API-like request paths.
6. Add tests that invoke the handler with no provider configured and assert no driver/native/Qdrant/mock dispatch occurs.

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
- There is exactly one production dispatch path for memory operations shared by tools and workflow executors.
- The handler records operation, source, context-delivery, and feedback correlation consistently.
- A temporary in-process native adapter is clearly isolated and cannot become the generic contract shape.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB15/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB15/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB15/manifest.md` and `proof/SB15/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run shared-handler tests proving tool and workflow executor requests produce matching operation lifecycle records.
- Run anti-duplication audit for multiple memory dispatch services or direct native calls.
- Run no-provider/capability-mismatch tests through tool, executor, context contributor, UI-like, and API-like handler callers.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB15 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB15 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
