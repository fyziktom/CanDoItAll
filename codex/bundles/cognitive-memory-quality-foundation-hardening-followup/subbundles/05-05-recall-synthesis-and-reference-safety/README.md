# 05-recall-synthesis-and-reference-safety

## Status

- `Completed`

## Objective

Turn recall synthesis from selected-section formatting into a grounded, concise consumer-facing brief with safe reference expansion.

## Success Criteria

- Synthesized briefs combine related selected memories into concise statements instead of copying the first line of each context section.
- Every synthesized statement has at least one included source ref unless the result explicitly reports no source-backed statements.
- References remain hidden by default.
- On-demand reference resolution returns locator/summary only when policy allows it.
- Unauthorized, redacted, or restricted references return typed exclusion reasons and no sensitive locator/summary content.

## Covered Inputs

- H-08, H-11, H-12, H-15.

## Prerequisites

- Subbundle 01 complete.
- Subbundle 04 complete and Gate D passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryRecallSynthesisService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryReferenceResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualitySupport.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallEvaluation.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs`

## Deliverables

- Focused recall synthesis implementation with deterministic test behavior.
- Tests proving synthesis is not merely first-line copying.
- Tests for statement/source-ref mapping, default hidden references, restricted/redacted reference exclusion, and no locator leakage.
- Preservation of the `SideContext` not promoted to `Selected` recall fix.

## Dependency Impact

- This is the consumer-facing layer of the original quality request. Final corpus proof cannot pass if synthesis remains a raw context dump.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add/enable tests that prove first-line formatting is insufficient.
2. Refactor synthesis logic into a focused component or helper that can merge related selected memories deterministically.
3. Preserve source refs per synthesized statement.
4. Harden reference resolver policy checks and exclusion payloads.
5. Run recall orchestrator tests to ensure `SideContext` candidates remain excluded from selected sections.

## Scope Exceptions

- Do not require a live LLM provider for synthesis.
- Do not change Blazor UI or agent-context presentation unless an explicit consumer integration test requires it.

## Do Not Do

- Do not include diagnostic scores, locators, or raw references in normal brief text.
- Do not expose restricted locator or summary values in excluded reference results.
- Do not regress the recall focus selection fix.

## Acceptance Checklist

- Synthesis tests prove concise merged statements.
- Reference resolver tests prove allow and deny paths.
- Recall orchestrator side-context test remains green.
- Persistence of synthesized statements/source maps remains green.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityFoundationTests|FullyQualifiedName~CognitiveMemoryRecallOrchestratorTests" --logger "console;verbosity=minimal" -m:1`
- Execution report includes a synthesized brief example and reference resolver deny-path result, with no sensitive values pasted into the report.

## Browser Validation Logging

- N/A unless implementation adds a UI route for synthesized recall display.

## Progression Gate

- Subbundle 06 and Subbundle 07 may proceed only after synthesis is proven as a grounded brief with safe on-demand references.

## Suggested Agent Prompt

```text
Implement subbundle 05 only. Improve recall synthesis and reference safety without changing UI. Preserve side-context behavior and record source-ref proof.
```
