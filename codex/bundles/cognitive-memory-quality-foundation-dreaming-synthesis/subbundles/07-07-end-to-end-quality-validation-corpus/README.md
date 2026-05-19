# 07 - End-to-End Quality Validation Corpus

## Status

- Status: `Completed`

## Objective

Build a representative validation corpus and prove the full loop: ingestion/source memories, clustering, dream aggregation, validation/review, activation, retrieval synthesis, and reference-on-demand.

## Covered Inputs

- Existing older validation reports.
- User concern that current validation does not prove deep dream behavior after P0/P1 refactors.
- All previous subbundles.

## Prerequisites

- Subbundles 02-06 implemented.
- Test infrastructure and optional Qdrant/Docker configuration understood.
- Any UI/API changes from earlier subbundles completed.

## Exact Source References

- C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\evidence\20260517-181521\99-run-summary.json
- C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\validation\evidence\20260517-115640\99-summary.json
- C:\repositories\CanDoItAll\docs\cognitive-memory\operations\validation-and-testing.md
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryConsolidationEngineTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryPersistenceModelTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CognitiveMemoryReviewUiPlaywrightTests.cs
- C:\repositories\CanDoItAll\docs\cognitive-memory\current-state\implementation-map.md
- C:\repositories\CanDoItAll\docs\cognitive-memory\architecture\runtime-flows.md

## Deliverables

- Validation corpus with multiple projects, source types, duplicates, contradictions, temporal updates, access/restricted content, and unrelated distractor memories.
- End-to-end tests that force explicit dream runs and assert quality metrics.
- Tests proving aggregate memories activate only after validation/review.
- Tests proving synthesized briefs are concise and references resolve correctly.
- Updated docs and execution report replacing old pre-P0/P1 assumptions.
- Optional Playwright proof for review/synthesis UI surfaces.

## Dependency Impact

- This is the closure bundle for the whole initiative.
- It may create repair tasks if any previous subbundle did not meet proof quality.

## Validation Depth

- Include non-happy paths, not only happy paths.
- Validate project boundaries and restricted content in aggregate and synthesized outputs.
- Assert work-depth metrics so shallow dream runs cannot pass.
- Compare old validation report assumptions with new implementation behavior.

## Implementation Steps

1. Create test data generator or fixtures for representative memory scenarios.
2. Run source ingestion/incremental consolidation where needed.
3. Run cluster planner and explicit dream run.
4. Validate aggregate candidates and process review decisions if required.
5. Activate approved aggregate memories.
6. Run recall synthesis and reference resolver.
7. Capture metrics, test output, API payload snapshots, and UI evidence if applicable.
8. Update docs and final execution report.

## Scope Exceptions

- Performance benchmarking can be lightweight; correctness and proof quality are more important.
- Full production scheduling is not required.

## Do Not Do

- Do not mark the initiative complete using only old validation bundle evidence.
- Do not skip contradiction, temporal, or restricted-content cases.
- Do not accept aggregate memories without source maps just to make tests pass.

## Acceptance Checklist

- [x] Regression corpus covers duplicates, contradictions, temporal updates, project boundaries, restricted content, and distractors.
- [x] End-to-end tests prove clustering, dreaming, validation, activation, synthesis, and references.
- [x] Dream run metrics prove non-shallow behavior.
- [x] Docs reflect the new architecture.
- [x] Execution report contains commands, evidence, and any remaining risks.

## Proof Required

- Unit/integration test commands and output.
- Example dream run quality report.
- Example aggregate memory with claim-source maps.
- Example synthesized brief and reference expansion.
- Playwright screenshots/traces if UI changed.


## Browser Validation Logging

- Record browser validation only when this subbundle changes UI-rendered behavior.
- If no UI changes are made, record `Not applicable - API/domain-only change` in the execution report.
- If UI changes are made, capture route, viewport, screenshots, console errors, and Playwright MCP trace/evidence.

## Progression Gate

- Do not proceed to the next subbundle until all acceptance checks pass or a blocker is documented with a safe rollback/repair plan.

## Suggested Agent Prompt

Use the shared implementation prompt, then execute this subbundle only. Read the exact source references first, implement the deliverables, run the required tests, and update the execution report with proof before moving on.
