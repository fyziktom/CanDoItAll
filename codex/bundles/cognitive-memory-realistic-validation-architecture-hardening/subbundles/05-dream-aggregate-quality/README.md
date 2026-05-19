# 05-dream-aggregate-quality

## Status

- `Ready`

## Objective

Make dream aggregates specific enough for human approval and safe enough to reject early when they are only structural summaries.

## Required Edits

- Build aggregate titles and bodies from primary keys plus source-backed snippets.
- Add a quality gate for structural-only or redacted-only aggregate candidates.
- Audit aggregate review decisions and application outcomes.

## Closure Proof

- At least one aggregate candidate contains concrete source-backed facts and is approved.
- Structural-only candidates are rejected or blocked with a clear reason.

## Covered Inputs

- Dream aggregate candidates were source-mapped but too generic after restricted redaction and could not be safely approved.

## Prerequisites

- Dream consolidation can access cluster records and source evidence while respecting restricted text handling.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamConsolidationService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs`

## Deliverables

- Aggregate titles and canonical text that include concrete, source-backed, policy-safe detail and reject structural-only summaries.

## Dependency Impact

- Long-run approval cycles depend on dream candidates being specific enough to accept or reject predictably.

## Validation Depth

- Unit tests must prove aggregate output includes concrete evidence while avoiding restricted text leakage.

## Implementation Steps

- Build aggregate text from cluster keys and evidence snippets, add specificity checks, and record review/application outcomes.

## Do Not Do

- Do not copy restricted raw source text into candidate text when the policy does not permit it.

## Acceptance Checklist

- Aggregate candidate names are specific to the cluster subject.
- Canonical text includes multiple source-backed facts.

## Proof Required

- Focused quality foundation tests covering aggregate specificity and redaction safety.

## Browser Validation Logging

- Record large-screen dream/quality tab proof when aggregate review controls are changed.

## Progression Gate

- Proceed only when aggregate candidates are specific, source-backed, and policy-safe enough for controlled review.

## Suggested Agent Prompt

- Improve Cognitive Memory dream aggregate generation so approval candidates contain concrete evidence instead of structural-only summaries.
