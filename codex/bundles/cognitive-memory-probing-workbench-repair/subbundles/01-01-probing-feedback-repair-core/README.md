# 01-probing-feedback-repair-core

## Status

- `Completed`

## Objective

- Make probe feedback operationally useful by creating review-gated repair candidates that can add or update canonical memory after explicit approval.

## Success Criteria

- Correction/incorrect/wrong-scope feedback creates a pending review item and a linked repair candidate.
- Approving that review item creates or updates a canonical memory record through existing review application behavior.
- Confirm/important feedback records evidence/calibration without mutating active truth.
- Regression test creation remains available from a probe turn.

## Covered Inputs

- R-003, R-004, R-005, R-006.
- Raw notes N001 and N002 for backend scope.

## Prerequisites

- Prepared-stage bundle validation must pass.
- Existing `ICognitiveMemoryProbeService` and `ICognitiveMemoryReviewUiService` source references must be inspected.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryReviewUiServiceTests.cs`

## Deliverables

- Probe feedback repair candidate generation.
- Review-linked consolidation candidate or equivalent typed repair path.
- Tests for no direct truth mutation and approval-only repair.
- Regression test creation still covered.

## Dependency Impact

- `02-dialogue-workbench-ui-and-validation` depends on this phase. UI correction controls are misleading unless approval actually repairs memory.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Inspect probe feedback and consolidation candidate payload types.
2. Add a typed helper that converts correction feedback into a review-linked repair candidate.
3. Keep direct canonical memory mutation impossible during feedback submission.
4. Ensure review approval applies the candidate through the existing review service path.
5. Add or update targeted unit tests.

## Scope Exceptions

- Generated probe-question queues are not owned by this subbundle.
- Rich required/forbidden regression constraints may remain follow-up if expected-evidence replay continues to work.

## Do Not Do

- Do not build UI in this phase.
- Do not bypass review for correction feedback.
- Do not create a second unrelated memory mutation engine.
- Do not add realistic source data to automated test code.

## Acceptance Checklist

- Feedback action `AddCorrection` creates a review item with candidate preview: `Passed`.
- Approval of that review item creates/updates memory: `Passed`.
- Feedback submission does not directly create canonical memory: `Passed`.
- Regression test creation still persists a test case linked to the probe turn: `Passed`.

## Proof Required

- Targeted unit tests for `CognitiveMemoryAdvancedServicesTests` and `CognitiveMemoryReviewUiServiceTests`.
- Optional API smoke if needed to verify PostgreSQL review application.

## Proof Captured

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemoryAdvancedServicesTests|FullyQualifiedName~CognitiveMemoryReviewUiServiceTests" --no-restore -m:1`
- Result: `Passed`, 10 tests, 0 failed.

## Browser Validation Logging

- N/A. Backend-only phase.

## Progression Gate

- `Passed`. Targeted tests prove review approval applies a probe correction repair candidate, so `02-dialogue-workbench-ui-and-validation` can start.

## Suggested Agent Prompt

```text
Implement this subbundle only. Make probe feedback create review-gated repair candidates and prove approval applies the repair through existing Cognitive Memory review/application paths. Do not build the UI or directly mutate canonical memory from feedback.
```
