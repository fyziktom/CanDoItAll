# 28 Native Maf Curator Professor Integration

## Status

- `Completed`

## Completion Summary

- Implemented native-owned curator, professor, self-regulation, policy-gate, event-emission, and context-contribution flows in `native-repo://src/CanDoItAll.CognitiveMemory.Maf`.
- Added focused production-DI tests for allowed and denied verification events, maintenance-signal emission, provider re-entry loop protection, and recall-backed MAF context contribution.
- Captured proof in `bundle://proof/SB28/manifest.md` and `bundle://proof/SB28/semantic-invariants.md`.

## Objective

- Move native curator/professor/self-regulation agent integrations into the native service MAF package using MAF abstractions only.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R15
- R16

## Prerequisites

- SB27 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorReviewService.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemorySelfRegulationOrchestrator.cs`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Implement native curator, professor, probing, review, self-regulation, and learning agents inside the native service/repo using MAF abstractions only where required.
- Ensure native agents are not stored or resolved through the main CanDoItAll Agent module.
- Expose native agent outcomes as provider events, native advanced UI operations, or protocol operation results according to capability policy.
- Add policy guards for memory-initiated verification requests and agent/workflow launch requests.
- Add tests for curator/professor flows, policy denial, event emission, and loop guard integration.

## Dependency Impact

- Native advanced behavior and event pushing depend on this package without reintroducing main module coupling.

## Validation Depth

- `Native MAF integration`

## Implementation Steps

1. Identify current advanced native MAF integration classes and move native-only logic into `CanDoItAll.CognitiveMemory.Maf` or native application services.
2. Register native internal agents using MAF abstractions/runtimes without referencing main Agent module storage.
3. Map native hypotheses, verification requests, and professor accepted-use signals to generic provider event envelopes.
4. Add policy checks for whether native memory may ask host agents/workflows to verify or act.
5. Add tests for allowed verification request, denied verification request, repeated event loop guard, and native advanced UI entry point.

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
- Native Cognitive Memory can run its own curator/professor flows without requiring main Agent module dependencies.
- Memory-initiated events are policy-gated and route through generic event inbox/outbox.
- No memory-agent-memory loop can be triggered by native event emission in tests.

## Proof Required

- Create `proof/SB28/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run native-service build/test commands from the `CanDoItAll.CognitiveMemory` repository after confirming real target paths, and capture transcript paths in the manifest.
- Run native MAF integration tests for curator/professor flows and provider event emission.
- Run dependency audit proving native MAF package references MAF abstractions but not the main Agent module implementation.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB28 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB28 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
