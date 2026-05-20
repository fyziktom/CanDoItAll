# 01 Curator Contracts And Capture Pipeline

## Status

- State: `Completed`
- Critical foundation: `Yes`

## Objective

Create the service contracts, persisted artifacts, and trusted-human capture path that lets curator conversation turns become high-confidence memory-improvement input while preserving recall trace ids and affected memory ids.

## Covered Inputs

- `R-004`, `R-005`, `R-006`, `R-007`
- Raw notes: automatic extraction, high priority/confidence, actor credit, approval bypass, and wrong-memory correction.

## Prerequisites

- Bundle prepared-stage validation must pass.
- Existing Cognitive Memory probe and consolidation code must remain available.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationCandidateApplicator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Neuro\CognitiveMemoryMutationAuthority.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Common\CognitiveMemoryJson.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs`

## Deliverables

- Strongly typed curator conversation enums, request/result records, and service interface.
- Persistence model for curator sessions/turns/captured improvement items or a documented minimal reuse of existing records.
- Capture pipeline that creates trusted source/evidence/mutation/consolidation artifacts with `RequiresHumanReview = false`.
- Metadata that records actor id, capture kind, priority/confidence, curator turn id, recall trace id, and affected memory ids.
- Tests that prove normal probe feedback still remains review-gated.

## Dependency Impact

- Subbundle 02 depends on this shared contract to drive both runtime modes.
- Subbundle 03 depends on the contract for UI state.
- If affected memory ids are missing, downstream correction proof is invalid.

## Validation Depth

- Deep unit/persistence validation.
- This is the critical foundation for the whole feature.

## Implementation Steps

1. Add strongly typed curator contracts and capture kinds.
2. Add or reuse persisted records for session/turn/captured improvement state.
3. Implement capture classification for explicit correction/new-knowledge user turns.
4. Persist trusted source manifest, source item, evidence anchor, mutation command, and consolidation candidate artifacts.
5. Register the service in DI.
6. Add focused unit tests for capture metadata, approval bypass, affected memory ids, and probe behavior isolation.

## Scope Exceptions

- Streaming responses are not part of this subbundle.
- Browser UI is handled in subbundle 03.

## Do Not Do

- Do not change existing probe feedback to globally skip review.
- Do not store mode/action/kind values as raw magic strings when an enum or contract can carry them.
- Do not create memory artifacts without source/evidence provenance.

## Acceptance Checklist

- Curator capture contracts compile.
- Trusted correction/new-knowledge capture creates source/evidence/mutation/candidate artifacts.
- Captured corrections include previous turn, recall trace, and affected memory ids when available.
- Curator mutation commands bypass manual review only in curator mode.
- Existing probe feedback tests still assert review-required behavior.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter CognitiveMemory`
- Relevant new unit test names recorded in `reviews/01-execution-report.md`.

## Browser Validation Logging

- N/A. This subbundle is backend-only.

## Progression Gate

- Pass only when tests prove the trusted capture path and affected-memory targeting work.
- Downstream runtime/UI work must stop if this gate fails.

## Suggested Agent Prompt

Implement subbundle 01 only. Add strongly typed curator conversation contracts and a trusted capture pipeline. Preserve normal probe review behavior. Prove correction capture stores recall trace ids and affected memory ids.
