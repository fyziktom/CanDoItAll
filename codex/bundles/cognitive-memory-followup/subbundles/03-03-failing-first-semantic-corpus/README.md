# SB03 - Failing-first semantic corpus

## Status

- Status: `Completed`

## Objective

Add adversarial tests that fail against the current shallow implementation before changing clustering, dreaming, professor, or recall production code.

## Covered Inputs

- Current tests are too narrow and often assert non-empty output, absence of diagnostic strings, or known fixture behavior.
- Feature subbundles need a negative corpus that captures the user intent.

## Prerequisites

- SB01 and SB02 completed; proof manifest validator active.

## Exact Source References

- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs

## Deliverables

- Add tests for bridge overmerge, contradiction-only relations, paraphrased clustering, and high-fanout fallback.
- Add tests for integrated dream synthesis, unsupported token-overlap claim rejection, and mode-specific structure.
- Add tests for natural professor capture, structured anchors, non-descendant support, and fading direct quote memory.
- Add tests for task-shaped recall brief, conflict separation, and precise statement-to-claim lineage.

## Dependency Impact

- Blocks SB04-SB08 production changes.
- SB04-SB08 must cite these failing-first tests in their proof manifests.

## Validation Depth

- Run targeted tests before production changes and capture failing transcript.
- Do not edit production cognitive-memory files except test helpers required to compile tests.
- Each test must assert semantic behavior, not only strings/counts.

## Implementation Steps

- Create test methods with explicit names listed in this bundle.
- Run them against current production code and capture failure transcript.
- Update `proof/SB03/manifest.md` with failing tests and expected repair owner SB04-SB08.
- Do not weaken tests to pass current implementation.

## Do Not Do

- Do not use `[Skip]` or equivalent.
- Do not assert only `NotEmpty`, `Contains`, or count increases.
- Do not rely on live LLM output.

## Acceptance Checklist

- At least one failing-first test exists for each of SB04, SB05, SB06, SB07, and SB08.
- Failing-first transcript exists and is validated by SB02 rules.
- No production cognitive-memory feature source changed in SB03.

## Proof Required

- `proof/SB03/manifest.md` with test file hashes and failing transcript.
- Transcript of targeted test run exiting non-zero for expected new tests.
- Source diff proving production feature files unchanged.

## Browser Validation Logging

- N/A - backend tests only.

## Progression Gate

- SB04-SB08 may start only after failing-first tests are committed and recorded.
- If tests pass before implementation, they are too weak and must be rewritten.

## Suggested Agent Prompt

Implement SB03. Add adversarial tests that demonstrate the current shallow behavior fails before any feature implementation.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
