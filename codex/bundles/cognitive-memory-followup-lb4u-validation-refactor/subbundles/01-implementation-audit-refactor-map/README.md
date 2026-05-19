# 01 Implementation Audit Refactor Map

## Status

- Status: `Completed`

## Objective

Verify the actual cognitive-memory implementation against the original v2 contract and produce a precise refactor map before coding.

## Covered Inputs

- Original bundle requirements and architecture.
- Current cognitive-memory module and Web API.
- Current unit, integration, component, and Playwright tests.
- Codeanalytics snapshot `snap-20260518225923-20ac6533`.

## Prerequisites

- Subbundle 00 baseline must be complete.
- Current source inventory must be refreshed if files changed after bundle preparation.
- No refactor should occur until tests/gaps are mapped.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs
- C:\repositories\CanDoItAll\tests

## Deliverables

- Updated implementation gap list.
- Exact test coverage map for each cognitive-memory capability.
- Refactor target list with file ownership and risk level.
- Decision on which behavioral fixes must precede maintainability splits.

## Dependency Impact

- Feeds subbundles 03, 04, 05, 06, and 07.
- Must avoid changing code directly unless a trivial inventory/test correction is required and validated.
- Prevents maintainability refactor from racing ahead of behavioral tests.

## Validation Depth

- Static source inspection.
- Test inventory.
- Optional codeanalytics refresh.
- Targeted test dry-run where cheap and safe.

## Implementation Steps

1. Re-read original v2 normalized requirements and execution report.
2. Refresh largest cognitive-memory files and route inventory.
3. Map existing tests to capability families.
4. Identify behavior gaps that need tests.
5. Identify file split candidates and shared helper candidates.
6. Update traceability and workbook with final edit map.

## Do Not Do

- Do not split large files yet.
- Do not add model-assisted behavior yet.
- Do not assume an endpoint is correct because it exists.
- Do not add abstractions without a concrete downstream use.

## Acceptance Checklist

- Original contract mapped to current source and tests.
- Missing behavior proof documented.
- Refactor map is narrow and sequenced.
- High-risk files have validation-before-refactor gates.

## Proof Required

- Source/test inventory notes.
- Updated workbook rows for refactor targets.
- Execution report update.
- Any command outputs for tests or codeanalytics refresh.

## Execution Proof

- Current implementation gaps were validated against the original v2 contract and the live LB4U test loop.
- The SQLite recall `DateTimeOffset` ordering failure was fixed by materializing lexical snapshots before sorting.
- Refactor targets were narrowed to behavior-backed splits: external-source extraction, staged-source manifests, model execution profiles, consolidation fact extraction, and probe summary redaction.
- Final validation commands passed: unit Cognitive Memory 113/113, integration Cognitive Memory 25/25, component Cognitive Memory 1/1, and serial solution build.

## Browser Validation Logging

- Browser validation is not required for this audit.
- If UI route inventory is inspected in browser, log route and viewport.

## Progression Gate

- Proceed to implementation subbundles only after the final refactor map and behavior gaps are recorded.

## Suggested Agent Prompt

Audit cognitive memory against the original v2 contract. Produce a precise gap/refactor map and update the workbook. Do not refactor yet.
