# 05 - Dream Validation And Review Gates

## Status

- Status: `Completed`

## Objective

Add multi-step validation and review gates that prevent weak, contradictory, stale, or unsafe aggregate memories from becoming active without review.

## Covered Inputs

- User concern that memory may be organizing/validating too shallowly.
- Current mutation authority and review flow.
- Aggregate claim provenance from Subbundle 04.

## Prerequisites

- Subbundle 04 claim-level source maps completed.
- Existing review UI/service behavior understood.
- Score geometry dimensions and answer gate behavior reviewed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Neuro\CognitiveMemoryMutationAuthority.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAnswerGateService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallScoring.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationServices.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CognitiveMemoryReviewUiPlaywrightTests.cs

## Deliverables

- Dream validation service with source coverage, independent support, contradiction, temporal/staleness, redaction/access, and generated-synthesis checks.
- Validation result records or trace objects tied to aggregate candidates.
- Review item support for aggregate candidates with source/claim alignment preview.
- Mutation authority integration that requires validation pass or review approval.
- Tests for approve/reject/review/defer paths.
- Optional UI improvements for aggregate review if needed.

## Dependency Impact

- Subbundle 06 depends on validated aggregate memories and confidence/caveat metadata.
- Subbundle 07 depends on validation traces and review proof.

## Validation Depth

- Tests must cover contradictory source memories, weak single-source evidence, stale/superseded data, redacted source text, and high-risk generated synthesis.
- Tests must assert that invalid aggregate candidates do not become active records.
- If UI changes, Playwright must verify the review surface and no console errors.

## Implementation Steps

1. Define validation request/result contracts.
2. Implement validation checks using aggregate claim/source maps.
3. Integrate validation with mutation authority and review item creation.
4. Add aggregate review previews showing statements and references.
5. Add tests for happy and non-happy paths.
6. Update docs and execution report.

## Scope Exceptions

- Review UI can be minimal if API-level validation and review flow are complete.
- Professor review can be represented as a required operation if a full reviewer workflow is not yet implemented.

## Do Not Do

- Do not approve aggregate memories solely because they have any evidence anchor.
- Do not hide contradiction/staleness warnings.
- Do not skip redaction/access checks for generated aggregate text.

## Acceptance Checklist

- [x] Dream validation service exists and is tested.
- [x] Mutation authority blocks weak/unsafe aggregates.
- [x] Review items show enough provenance for reviewers.
- [x] Non-happy paths are covered by tests.
- [x] UI proof exists if UI changed.

## Proof Required

- Unit tests for validation decisions.
- Review service tests for aggregate candidates.
- Playwright evidence if review UI changed.
- Example validation trace for approved and rejected aggregate.


## Browser Validation Logging

- Record browser validation only when this subbundle changes UI-rendered behavior.
- If no UI changes are made, record `Not applicable - API/domain-only change` in the execution report.
- If UI changes are made, capture route, viewport, screenshots, console errors, and Playwright MCP trace/evidence.

## Progression Gate

- Do not proceed to the next subbundle until all acceptance checks pass or a blocker is documented with a safe rollback/repair plan.

## Suggested Agent Prompt

Use the shared implementation prompt, then execute this subbundle only. Read the exact source references first, implement the deliverables, run the required tests, and update the execution report with proof before moving on.
