# 05 Deep Dreaming Validation And Calibrated Apply

## Status

- `Completed`

## Objective

- Turn dreaming from template aggregation into structured claim synthesis with claim-level validation and cautious aggregate application.

## Success Criteria

- Aggregate canonical memory is domain-useful and does not contain internal diagnostic boilerplate as memory content.
- Each synthesized claim has source maps that actually support that claim.
- Conflicting claims are separated into caveats/review frames, not overconfident aggregates.
- Duplicate/near-duplicate aggregates are detected by claim/source signature, not only title.
- Aggregate application uses calibrated confidence and does not default to strong accept from shallow approval.

## Covered Inputs

- Current dream canonical text is template text with cluster quality and shared signals.
- Current validator checks counts and flags more than semantic claim support.
- Current aggregate apply can promote approved candidates to strong active memory too easily.

## Prerequisites

- SB03 dream/apply tests present.
- SB04 composite clustering completed.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Consolidation/CognitiveMemoryConsolidationCandidateApplicator.cs

## Deliverables

- Claim extraction/normalization pipeline for dream source memories.
- Aggregate candidate builder that synthesizes domain-level canonical text and claim records.
- Validator checks for claim support, near duplicates, source independence, conflict, curator invalidation, and generated-only ancestry.
- Calibrated apply path with probationary/weak states unless strong criteria are met.
- Tests for canonical text quality, unsupported claims, conflicts, duplicates, and confidence calibration.

## Dependency Impact

- Blocks recall synthesis because recall needs useful aggregate memories.
- Blocks professor assimilation because anchors should compare against real aggregates, not template text.

## Validation Depth

- Critical implementation with semantic and adversarial tests.
- No live LLM calls are required; deterministic structured synthesis is acceptable if behavior proof is strong.

## Implementation Steps

1. Create normalized claim units from memory records and evidence anchors.
2. Group equivalent claims and detect conflicting claim groups.
3. Generate canonical aggregate text from supported claim groups without internal diagnostic boilerplate.
4. Attach source maps per claim, not only per candidate.
5. Strengthen validator to reject or review unsupported, mixed-topic, duplicate, restricted, generated-only, and curator-invalidated candidates.
6. Fix confidence calibration and avoid default strong accept for ordinary dream aggregates.
7. Run existing and new dream/apply tests.

## Scope Exceptions

- Deep generative rewriting may be deferred if deterministic synthesis produces useful domain claims.
- Exact confidence thresholds may be tuned, but the tests must prove weak evidence cannot become strong accept.

## Do Not Do

- Do not keep `Synthesized aggregate:` and `Cluster quality:` as canonical memory content.
- Do not claim support because a source map exists; verify content relationship at least through normalized claim/signature overlap.
- Do not mark ordinary machine-generated aggregates as `StrongAccept` by default.

## Acceptance Checklist

- Dream canonical text quality test passes.
- Unsupported synthesized claim test fails/reviews as expected.
- Near-duplicate aggregate test passes.
- Confidence calibration test shows weak evidence stays weak/probationary.
- Execution report includes anti-stub audit for template text.

## Proof Required

- Targeted dream/apply unit tests.
- Command output for full cognitive-memory quality tests.
- Execution report semantic proof section with example aggregate text before/after.
- No browser proof unless UI changed.

## Browser Validation Logging

- N/A unless quality UI displays aggregate text/status changes.
- If UI changes, capture `/cognitive-memory` Quality tab large-screen screenshot and review readability.

## Progression Gate

- SB06 and SB07 may proceed only after aggregate text, validation, duplicate, and confidence tests pass.
- If canonical aggregate content remains diagnostic-template-first, this gate fails.

## Suggested Agent Prompt

```text
Implement deep dream claim synthesis, validation, and calibrated apply. Remove diagnostic boilerplate from canonical memory and prove unsupported/duplicate/weak aggregates cannot pass as strong stable knowledge.
```

## Execution Proof

- Implemented deterministic claim-unit synthesis in `CognitiveMemoryDreamConsolidationService`: canonical aggregate text is now built from domain claims and claim records carry their own source maps.
- Strengthened `CognitiveMemoryDreamValidator`: unsupported mapped claims are reviewed, duplicate detection compares generated aggregate claim/source signatures, and generated-only/restricted/contradictory gates remain enforced.
- Calibrated `CognitiveMemoryAggregateMemoryApplicator`: ordinary approved dream aggregates now apply as `WeakAccept` / `Experimental` unless strong per-claim source agreement is present.
- Targeted proof passed: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate|FullyQualifiedName~DreamValidation_RoutesUnsupportedMappedClaimToReview|FullyQualifiedName~DreamValidation_DetectsNearDuplicateAggregateByClaimAndSourceSignature|FullyQualifiedName~AggregateApplicator_KeepsOrdinaryDreamAggregateWeakAndExperimental|FullyQualifiedName~DreamRun_ProjectNightlyCreatesApprovedCandidateAndMetrics" --logger "console;verbosity=normal"` passed `5/5`.
- Quality foundation proof for SB05 surface passed: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemoryQualityFoundationTests&FullyQualifiedName!~RecallSynthesis_BuildsQueryShapedBriefInsteadOfTitleGroupedConcatenation" --logger "console;verbosity=minimal"` passed `25/25`; the excluded recall test is the known SB07 regression-first gate.
- Browser validation: not required; no UI changed.
