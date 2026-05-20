# SB06 - Natural professor capture and structured anchors

## Status

- Status: `Completed`

## Objective

Make curator/professor mode capture natural teaching conversations as structured temporary anchors rather than only keyword-triggered direct memories.

## Covered Inputs

- Current capture uses explicit kind or keyword heuristics.
- Current capture stores mostly raw user text as trusted memory.
- User wants comfortable professor-style learning without approving a flood of proposals.

## Prerequisites

- SB03 professor capture tests fail first.
- SB04 cluster signals available.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs

## Deliverables

- Add professor teaching extractor for natural multi-turn guidance.
- Add structured professor anchor model: claims, target scope, misconception corrected, source utterances, confidence, lifecycle state.
- Add capture kinds or subtypes for teaching answer, confirmation, misconception correction, scope correction, and new knowledge.
- Separate temporary professor anchor memory from ordinary stable recalled knowledge.
- Integrate professor anchors into cluster/dream comparison as privileged review signals, not automatic truth.

## Dependency Impact

- Feeds SB07 assimilation and fading.
- Improves SB04/SB05 with professor anchor aliases and review signals.
- May require UI display changes if curator tab exposes anchor state.

## Validation Depth

- Natural conversation tests without `remember`, `wrong`, or `learn` keywords must capture structured anchors.
- Multi-turn teaching must produce extracted claims and target scope.
- Active anchors must not appear as ordinary stable recall knowledge by default.
- Ambiguous correction must still require explicit target selection.

## Implementation Steps

- Design anchor claim extraction DTO/entities or JSON payload with migration/configuration.
- Implement deterministic extractor using rules/provider interface with test fake.
- Update curator capture service to create temporary anchors and direct capture memory with appropriate stability/visibility.
- Add policy to exclude active anchors from ordinary recall unless explicitly requested or used for comparison/review.
- Persist algorithm version update.

## Do Not Do

- Do not require the user to use command words in normal professor mode.
- Do not immediately treat all professor text as permanent stable knowledge.
- Do not supersede unrelated recalled memories from an ambiguous correction.

## Acceptance Checklist

- Natural professor explanation creates structured anchor claims.
- Anchor includes target scope and source utterance lineage.
- Active anchor is visible in curator/learning state but not default stable recall.
- Ambiguous target correction creates review item and no broad supersede.

## Proof Required

- `proof/SB06/manifest.md`.
- Targeted curator/professor tests.
- Transcript proving natural non-keyword capture.
- Source-level assertion for structured anchor creation and recall exclusion.

## Completion Proof

- Manifest: `proof/SB06/manifest.md`
- Passing targeted tests: `proof/SB06/transcripts/passing-targeted-professor-anchor-tests.txt`
- Regression tests: `proof/SB06/transcripts/passing-professor-regression-tests.txt`
- Source assertions: `proof/SB06/transcripts/source-assertions.txt`
- Anti-stub audit: `proof/SB06/transcripts/anti-stub-audit.txt`

## Browser Validation Logging

- Run component/browser proof if curator UI surfaces anchor state or target-selection changes.

## Progression Gate

- SB07 cannot start until structured anchors and default recall exclusion are proven.
- If capture still depends only on keywords or explicit capture kind, SB06 remains incomplete.

## Suggested Agent Prompt

Implement SB06. Build natural professor capture and structured temporary anchors with safe recall behavior.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
