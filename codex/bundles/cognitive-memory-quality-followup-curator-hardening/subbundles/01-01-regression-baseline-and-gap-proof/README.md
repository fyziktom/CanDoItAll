# 01 Regression Baseline And Gap Proof

## Status

- Status: `Ready for implementation`

## Objective

Create an objective failing-then-passing regression baseline that proves the current gaps before deeper implementation work starts.

## Covered Inputs

- Current review findings F-01 through F-10.
- User concern that dreaming is suspiciously fast and likely shallow.
- User concern that curator/professor mode must improve memory rather than only create manual captures.

## Prerequisites

- Use the extracted current implementation as the baseline.
- Do not refactor production services before adding the targeted regression tests for this subbundle.

## Exact Source References

- /mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- /mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- /mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallEvaluation.cs

## Deliverables

- New or updated unit tests for low-signal clusters, shallow dream text, broad curator supersede, Czech curator phrases, and recall brief/reference behavior.
- A short baseline note in the execution report documenting which tests fail before production changes, where feasible.
- Updated weak tests that currently assert broad key family clusters so they assert quality gates instead.

## Dependency Impact

- Unlocks SB02, SB04, and final closure proof.
- If this subbundle is weak, later work can pass by preserving the same shallow behavior.

## Validation Depth

- Run the targeted Cognitive Memory unit tests before and after implementation changes.
- Tests must include non-happy paths, not only happy-path plumbing.
- No browser validation is required unless the regression is component/UI-visible.

## Implementation Steps

- Add cluster regression seeds from `templates/regression-scenarios.md`.
- Add dream regression seeds proving copied-list candidates are insufficient.
- Add curator regression seeds for multiple recalled memories and Czech/English capture language.
- Add recall synthesis regression seeds for concise brief and reference expansion.
- Record initial/final outcomes in `reviews/01-execution-report.md`.

## Scope Exceptions

- Do not require full professor assimilation implementation in this subbundle; only create tests or pending/skipped markers that downstream subbundles close.
- Do not include economic memory governance tests.

## Do Not Do

- Do not delete existing plumbing tests; update them where they assert harmful behavior.
- Do not use live LLM/provider calls for deterministic regression proof.

## Acceptance Checklist

- Low-signal-only cluster test exists and fails on old behavior/passes after SB02.
- Broad curator correction test exists and fails on old behavior/passes after SB04.
- Recall synthesis/reference tests exist and are owned by SB06 if not closed immediately.
- Execution report lists test commands and outcomes.

## Proof Required

- Targeted `dotnet test` commands for unit tests.
- Component test proof only if UI-visible assertions are added.
- Execution report gate row updated.

## Browser Validation Logging

- N/A unless component tests or browser-visible UI regression is added in this subbundle.

## Progression Gate

- Proceed to SB02 only when the clustering regression tests are present.
- Proceed to SB04 only when curator broad-target regression tests are present.

## Suggested Agent Prompt

Add a regression-first baseline for the Cognitive Memory quality gaps. Focus on broad low-signal clusters, shallow dream candidates, broad curator supersede, Czech curator phrase handling, and recall synthesis/reference behavior. Keep tests deterministic and update existing weak assertions.
