# 04 Curator Professor Anchor Capture And Targeting

## Status

- Status: `Completed`

## Objective

Make curator/professor capture structured, multilingual enough for Czech/English usage, and safe against broad accidental supersede/refine.

## Covered Inputs

- F-07 curator captured but not professor-mode learning.
- F-08 broad correction targeting risk.
- F-09 UI lacks target controls.
- RQ-07 and RQ-08.

## Prerequisites

- SB01 curator broad-target regression tests must exist.
- Keep source trust and mutation audit behavior intact.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryCuratorTab.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Curator.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs

## Deliverables

- Structured curator capture extraction model with capture kind, assertion text, scope, target memory ids, target claim ids, target confidence, language/source, and ambiguity state.
- Czech and English deterministic phrase baselines plus explicit UI/API override.
- Target resolver that does not treat all recall context memories as correction targets.
- Review/clarification path for ambiguous professor corrections.
- UI/API changes for explicit capture kind and target selection when needed.

## Dependency Impact

- Blocks SB05 assimilation lifecycle.
- Unsafe targeting invalidates all downstream professor-learning proof.

## Validation Depth

- Unit tests for explicit target correction, ambiguous multi-target correction, new knowledge, wrong scope, Czech phrases, and false positives.
- Component/UI tests for target/capture controls if added.
- Policy tests that restricted sources remain protected.

## Implementation Steps

- Extract capture classification from `CognitiveMemoryCuratorConversationService` into a testable component or equivalent.
- Add structured capture result and target resolver.
- Change correction path so recall trace records are candidate targets, not automatically affected records.
- Create review/clarification item for ambiguous targets.
- Update UI/page request path to pass explicit capture kind and selected targets where available.
- Keep trusted source item/evidence creation but avoid immediate destructive broad supersede.

## Scope Exceptions

- Do not fully implement anchor assimilation in this subbundle; create the capture/target model SB05 will use.
- Do not rely only on natural language heuristics when UI/API explicit target is available.

## Do Not Do

- Do not mark every included recall memory stale/superseded for one correction.
- Do not assume English-only curator input.

## Acceptance Checklist

- Correction with three included recall memories and no explicit target creates pending/review/clarification state, not three supersedes.
- Explicit target correction changes only the intended memory/claim.
- Czech phrases are recognized or routed to structured explicit capture without silent ignore.
- Component/browser proof exists for new UI controls if UI changed.

## Proof Required

- Targeted advanced service tests.
- Component test output for curator UI if changed.
- Execution report row updated.

## Implementation Evidence

- Added structured capture and target fields to API contracts and persisted capture records.
- Added explicit curator UI controls for capture kind, target memory ids, target claim ids, target confidence, and scope.
- Ambiguous corrections with multiple recalled memories create review state instead of broad supersede; Czech phrase baseline is deterministic.

## Browser Validation Logging

- Route: `/cognitive-memory` Curator tab.
- Large desktop viewport proof for explicit capture/target controls and ambiguity state.
- Narrow responsive smoke if controls affect layout.

## Progression Gate

- SB05 may start only when broad-supersede regression is closed and target model is available.

## Suggested Agent Prompt

Harden curator/professor capture and targeting. Introduce structured capture with explicit targets, Czech/English baselines, ambiguity handling, and UI/API target controls. Prevent broad automatic supersede from recall context.
