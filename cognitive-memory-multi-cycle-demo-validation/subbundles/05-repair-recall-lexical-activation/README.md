# Repair recall lexical activation

## Status

- `Completed`

## Objective

- Repair the recall path discovered during multi-cycle validation where later-stage memories were missed when useful query terms were not present in the first narrow lexical prefilter.

## Success Criteria

- Later-stage S02-S04 probes return source-backed context.
- Expected staged source locators appear for all 24 probes.
- Cross-project source locator leakage remains zero.
- The recall repair has focused unit coverage.

## Covered Inputs

- R7 backward memory quality analysis.
- R8 AI chat validation prerequisite.
- R9 on-the-fly repair subbundles.

## Prerequisites

- Subbundle 03 produced backward analysis showing under-selection.
- Main run evidence exists under `validation/evidence/20260517-181521`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\analyze-multi-cycle-memory-quality.ps1`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\run-recall-regression-after-repair.ps1`

## Deliverables

- Recall lexical fallback implementation.
- Unit test for scored project lexical scan fallback.
- Post-repair recall regression evidence.

## Dependency Impact

- Chat validation depends on recall selecting the correct project and stage memories. Without this repair, automatic and manual chat proof can pass only by accidentally selecting baseline memories or by over-injecting context.

## Validation Depth

- Critical repair gate.

## Implementation Steps

1. Reproduce the backward-analysis failure from `95-memory-quality-analysis.json`.
2. Inspect recall candidate activation and identify why later-stage sources are excluded.
3. Add stopword-aware term normalization.
4. Add a bounded project-level scored lexical scan when the initial lexical candidate set under-fills the recall budget.
5. Add unit coverage for first-term miss fallback.
6. Rerun all 24 staged recall probes.
7. Record post-repair locator and leakage counts.

## Scope Exceptions

- This repair does not configure vector projection providers.

## Do Not Do

- Do not bypass recall through direct database reads.
- Do not widen recall globally without project scoping.
- Do not accept later-stage recall unless locators map back to the staged source files.

## Acceptance Checklist

- Completed: Recall returns context for all 24 staged probes.
- Completed: Every probe contains the expected staged source locator.
- Completed: Cross-project locator count is zero.
- Completed: Unit test covers the fallback behavior.

## Proof Required

- `validation/evidence/20260517-181521-post-repair-recall-20260517-183324/post-repair-recall-summary.json`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentContextContributionTests|FullyQualifiedName~CognitiveMemoryRecallOrchestratorTests" --no-restore`

## Browser Validation Logging

- N/A. This subbundle repairs backend recall selection. Browser validation is recorded in Subbundles 02 and 03.

## Progression Gate

- Subbundle 04 may close only after post-repair recall reaches 24/24 expected staged source locators.

## Suggested Agent Prompt

```text
Implement this repair subbundle only.
Use the backward memory-quality analysis to identify why later-stage recall is under-selected. Repair recall selection without bypassing project scoping, add focused unit coverage, rerun the 24 staged recall probes, and record source-locator proof before returning to chat validation.
```
