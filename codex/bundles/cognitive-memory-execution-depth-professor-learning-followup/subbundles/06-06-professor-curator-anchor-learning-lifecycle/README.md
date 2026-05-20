# 06 Professor Curator Anchor Learning Lifecycle

## Status

- `Completed`

## Objective

- Implement the professor/student learning behavior where curator input becomes a durable high-trust anchor that is compared, applied, assimilated only through distinct derived proof, and faded only after internalization.

## Success Criteria

- Curator turns are parsed into structured professor anchor assertions, scope, targets, and confidence.
- A direct curator-applied memory cannot be used as assimilation proof for its own capture.
- Anchors can enter Comparing state when related clusters/memories are evaluated.
- Assimilation requires a distinct derived aggregate/memory/use observation with lineage back to the anchor plus independent support or repeated correct use.
- Fading keeps provenance available and happens only after assimilation.
- Anchor state influences cluster/dream review without over-dominating contradictory evidence.

## Covered Inputs

- Current curator service immediately applies trusted captures when targeting is not ambiguous.
- Current professor service only checks that a requested derived memory exists.
- Current test uses the same applied memory as derived proof, defeating the professor-learning model.

## Prerequisites

- SB03 professor tests present.
- SB04 composite clustering completed.
- SB05 dream validation and aggregate apply completed.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs

## Deliverables

- Professor anchor assertion model or equivalent structured persistence.
- Assimilation observation model or equivalent records proving distinct derived knowledge.
- Updated professor anchor service with state-machine validation.
- Dream/cluster integration so active anchors can guide comparison and invalidate stale aggregates.
- Tests for direct-capture self-assimilation rejection, independent derived assimilation, fading, correction targeting, and ambiguous review.

## Dependency Impact

- Blocks final recall proof because recall must be able to explain professor-derived knowledge and faded anchors.
- Prevents trusted user/professor input from becoming permanent overconfident memory without internalization proof.

## Validation Depth

- Critical foundation implementation with state-machine and adversarial tests.
- UI/component/browser proof is required if curator tab exposes new state controls or badges.

## Implementation Steps

1. Introduce structured anchor assertions from user/professor text, including assertion text, scope, target memory ids, target claim ids, language, and confidence.
2. Distinguish operational application from assimilation; an applied direct memory is not assimilated knowledge.
3. Add comparison workflow that links anchors to clusters, claims, contradictions, and dream candidates.
4. Implement assimilation proof rules requiring a distinct derived record/aggregate or repeated-use observation.
5. Implement fading rules that keep source lineage but retire raw-anchor criticality.
6. Update curator/professor tests and any UI needed to inspect anchor state safely.

## Scope Exceptions

- Natural-language parsing can begin with deterministic patterns and explicit API fields, but must store structured assertions and tests for Czech/English cases.
- Automatic scheduled assimilation may be implemented as an explicit service method first, if the state machine and proof are complete.

## Do Not Do

- Do not mark anchors assimilated using `capture.AppliedMemoryRecordId` for the same capture.
- Do not let ambiguous correction mutate multiple recalled memories.
- Do not fade anchors without retained provenance and derived proof.
- Do not treat professor input as low-trust ordinary source or as permanently unchallengeable truth.

## Acceptance Checklist

- Direct self-assimilation test rejects the operation.
- Distinct derived aggregate assimilation test passes.
- Fading-before-assimilation test fails as expected.
- Curator ambiguous and explicit target tests pass.
- Anchor integration affects dream/cluster review in at least one test.

## Proof Required

- Targeted advanced service tests.
- Full cognitive-memory unit test subset.
- Component/browser proof if curator UI state changed.
- Execution report includes professor state-machine proof.

## Browser Validation Logging

- If UI changed: route `/cognitive-memory`, Curator tab.
- Large desktop screenshot plus narrow responsive pass when controls/badges changed.
- Proof must show anchor state, target status, and review/assimilation status are readable.

## Progression Gate

- SB07 may proceed only when professor anchors can be referenced in recall lineage and cannot self-assimilate.
- If same direct capture can still be assimilation proof, this gate fails.

## Suggested Agent Prompt

```text
Implement the professor/student learning lifecycle. Separate direct trusted capture from true internalization, require distinct derived proof for assimilation, and prove fading only happens after assimilation.
```

## Execution Proof

- Updated `CognitiveMemoryProfessorAnchorService` so direct curator-applied memory cannot assimilate its own capture, and distinct derived memory must retain anchor lineage plus independent support before assimilation/fade.
- Reused existing structured capture persistence for anchor assertions: assertion summary/correction text, scope, target memory ids, target claim ids, language, confidence, source item id, and evidence anchor id.
- Updated dream validation so active professor-anchor source memories enter `Comparing` and force review when used in aggregate candidates before assimilation.
- Targeted proof passed: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProfessorAnchor_AssimilatesAndFadesOnlyAfterDerivedMemoryExists|FullyQualifiedName~ProfessorAnchor_DirectCaptureMemoryCannotAssimilateItsOwnAnchor|FullyQualifiedName~ProfessorAnchor_ActiveAnchorSourceMovesDreamCandidateToComparisonReview|FullyQualifiedName~CuratorCapture_CorrectionTargetsIncludedRecallMemoryAndSupersedesIt|FullyQualifiedName~CuratorCapture_AmbiguousCorrectionWithMultipleRecallMemoriesCreatesReviewWithoutBroadSupersede" --logger "console;verbosity=normal"` passed `5/5`.
- Full advanced subset passed: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemoryAdvancedServicesTests" --logger "console;verbosity=minimal"` passed `25/25`.
- Browser validation: not required; no UI changed.
