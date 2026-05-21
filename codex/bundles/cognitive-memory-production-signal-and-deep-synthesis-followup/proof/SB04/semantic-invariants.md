# SB04 Semantic Invariants

## Invariant SB04-COMPARISON-RESOLUTION-01

- Invariant ID: `SB04-COMPARISON-RESOLUTION-01`
- Source raw note: Professor anchors must not remain stranded in `Comparing` after human-reviewable dream validation.
- Expected behavior: `ResolveComparisonAsync` transitions `Comparing` anchors to `Assimilated`, `Faded`, `Active`, or `Rejected` based on an explicit review outcome.
- Disallowed shallow implementation: Auto-clearing `Comparing` without actor, reason, or outcome.
- Failing-first test: `SemanticInvariant_ProfessorComparisonReviewResolutionIsExplicitAndAudited` failed in the SB02 baseline.
- Passing test: `ProfessorComparisonReviewResolution_ReturnsComparingAnchorToActiveAndAuditsTransition`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorReviewService.cs`.
- Production assertions: The resolver rejects non-`Comparing` anchors and validates accepted derived memory is approved, active, same project, and not the direct capture memory.
- Red-team negative case: Invalid lifecycle state throws instead of silently hiding a bad transition.
- Downstream dependency check: Review resolution now has a production command surface.

## Invariant SB04-AUDITED-TRANSITION-02

- Invariant ID: `SB04-AUDITED-TRANSITION-02`
- Source raw note: Every comparison review transition must persist audit evidence.
- Expected behavior: Resolution calls `CognitiveMemoryProfessorAnchorTransitionAudit.AddTransition`.
- Disallowed shallow implementation: Mutating `AnchorState` without an audit signal.
- Failing-first test: SB02 red baseline lacked `ResolveComparisonAsync` and review-service audit use.
- Passing test: `ProfessorComparisonReviewResolution_ReturnsComparingAnchorToActiveAndAuditsTransition`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorReviewService.cs`.
- Production assertions: The audit metadata contains capture id, previous state, next state, manual review confirmation, and optional derived memory id.
- Red-team negative case: Audit helper ignores same-state no-ops, so resolution must perform an actual state transition.
- Downstream dependency check: Lifecycle history is available to diagnostics and final proof.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `Comparing` state resolution | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorReviewService.cs` | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorTransitionAudit.cs` | `bundle://proof/SB04/transcripts/anti-stub.txt` |
