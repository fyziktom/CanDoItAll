# 01-reentry-audit-and-regression-safety-net

## Status

- `Ready`

## Objective

Create the hardening safety net before behavior refactoring: document the specific phase-one gaps and add regression tests or explicit pending tests that fail against the current implementation for repeat execution, lifecycle, dry-run, mode-policy, policy, provenance, and synthesis weaknesses.

## Success Criteria

- Every critical review finding from `analysis/01-current-state.md` has a test, a pending test with reason, or an explicit exception.
- The test names describe the behavior contract, not the implementation detail.
- Tests are placed in existing unit/integration projects unless a new fixture is genuinely required.

## Covered Inputs

- H-01, H-03, H-05, H-06, H-07, H-09, H-10, H-11, H-12, H-15.
- User distrust of prior completion claim.
- Passing baseline tests that are insufficient for closure.

## Prerequisites

- None. This subbundle is the entry gate for all implementation work.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-quality-foundation-hardening-followup\analysis\01-current-state.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryQualityPersistenceModelTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryConsolidationPersistenceModelTests.cs`

## Deliverables

- A concise re-entry audit note or test-plan section under the follow-up execution report.
- Regression tests for repeated cluster planning, second dream run after existing clusters, dry run, unsupported mode, failure path, aggregate apply idempotency, redaction/reference safety, and non-trivial synthesis.
- Pending tests are acceptable only when the implementation dependency is named and the owning downstream subbundle is listed.

## Dependency Impact

- All downstream subbundles depend on this safety net. Without it, refactoring can preserve broken behavior or silently weaken the follow-up scope.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Read the current-state findings and last-commit tests.
2. Add regression tests in the smallest existing test project that can prove each behavior.
3. For behaviors that cannot be tested until a seam exists, add a skipped or pending test only if the skip reason names the downstream subbundle that must unskip it.
4. Run the targeted unit/integration slices and record which tests fail before implementation.
5. Update `reviews/01-execution-report.md` with the new safety-net rows.

## Scope Exceptions

- Do not change production implementation in this subbundle unless a test fixture needs a minimal helper.

## Do Not Do

- Do not refactor `CognitiveMemoryQualityServices.cs` yet.
- Do not remove existing passing tests.
- Do not collapse repeat-run and idempotency tests into a single count assertion.

## Acceptance Checklist

- Regression tests exist for all critical hardening gaps.
- Any pending test has an owning subbundle and explicit reason.
- Targeted tests have been run and results are recorded.
- Execution report identifies current failing tests as expected blockers for downstream work.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityFoundationTests|FullyQualifiedName~CognitiveMemoryRecallOrchestratorTests" --logger "console;verbosity=minimal" -m:1`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityPersistenceModelTests|FullyQualifiedName~CognitiveMemoryConsolidationPersistenceModelTests" --logger "console;verbosity=minimal" -m:1`
- Execution-report rows showing which tests fail before downstream fixes.

## Browser Validation Logging

- N/A. This subbundle changes test coverage and audit artifacts only.

## Progression Gate

- Downstream implementation may start only after every critical review finding is represented by a regression test, pending test, or explicit scoped exception.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Add the regression safety net first. Do not refactor production code unless required for test fixture setup. Record failing/pending tests in the execution report and stop before downstream fixes.
```
