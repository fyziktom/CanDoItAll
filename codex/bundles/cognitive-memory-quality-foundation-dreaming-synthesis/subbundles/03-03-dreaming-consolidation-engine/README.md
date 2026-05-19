# 03 - Dreaming Consolidation Engine

## Status

- Status: `Ready`

## Objective

Implement explicit dreaming runs that operate on clusters, not only individual source items. Dreaming should organize memories, create aggregate candidates, detect contradictions/duplicates/supersession, and produce quality metrics.

## Covered Inputs

- User concern that current dream mode is suspiciously fast.
- Current consolidation modes that exist but do not behave distinctly.
- Cluster substrate from Subbundle 02.

## Prerequisites

- Subbundle 02 cluster planner and membership persistence completed.
- Baseline diagnostics from Subbundle 01 available.
- Existing consolidation service behavior understood and preserved for incremental source processing.

## Exact Source References

- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Consolidation/CognitiveMemoryConsolidationServices.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Consolidation/CognitiveMemoryConsolidationContracts.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Consolidation/CognitiveMemoryConsolidationFactExtractor.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Consolidation/CognitiveMemoryConsolidationCandidateApplicator.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Neuro/CognitiveMemoryMutationAuthority.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryConsolidationEngineTests.cs

## Deliverables

- Explicit dream run service/API separate from or clearly layered above incremental consolidation.
- Mode-specific dream behavior for at least ProjectNightly, ProcedureMining, FailureLearning, and KnowledgeCoverageRefresh.
- Dream agenda selection from clusters with reason codes and quality metrics.
- Aggregate candidate creation from cluster members.
- Dream run quality report persisted or returned with stage timings and work-depth counts.
- Tests proving a dream run reads multiple cluster members and creates aggregate candidates.

## Dependency Impact

- Subbundle 04 uses aggregate candidates and dream run records.
- Subbundle 05 validates candidates produced here.
- Subbundle 07 uses dream metrics to detect shallow runs.

## Validation Depth

- Tests must prove dreaming does more than scanning source items.
- Tests must assert non-zero cluster members read, claims extracted, aggregate candidates created, and validation requests scheduled for a representative cluster.
- Tests must cover a shallow/no-op run and require explicit reason/metrics rather than silent success.

## Implementation Steps

1. Introduce dream run contracts and records or extend consolidation run records with explicit dream fields.
2. Load cluster plans from Subbundle 02.
3. Select dream agenda with deterministic priority rules.
4. Read cluster members and source/evidence details.
5. Extract candidate facts/claims using deterministic logic first; optionally allow provider-based synthesis behind an interface.
6. Create aggregate candidates without immediately activating them.
7. Persist/return quality metrics and update docs/tests.

## Scope Exceptions

- Fully autonomous scheduling is optional; explicit run request is enough for this subbundle.
- Generated text can be basic at first as long as claim/evidence mapping is prepared for Subbundle 04.

## Do Not Do

- Do not mutate active memory directly from a dream run without validation.
- Do not count a run as successful if it produced no cluster/dream metrics and no explicit no-op reason.
- Do not implement economic prioritization.

## Acceptance Checklist

- [ ] Dream run is distinguishable from incremental consolidation.
- [ ] At least four modes have distinct behavior or distinct agenda filters.
- [ ] Dream run quality metrics are persisted or returned.
- [ ] Aggregate candidates are cluster-derived.
- [ ] Tests prove multi-member dream behavior.

## Proof Required

- Unit tests for dream run agenda and quality metrics.
- Consolidation/dream tests showing aggregate candidates from clusters.
- Example dream run quality report.


## Browser Validation Logging

- Record browser validation only when this subbundle changes UI-rendered behavior.
- If no UI changes are made, record `Not applicable - API/domain-only change` in the execution report.
- If UI changes are made, capture route, viewport, screenshots, console errors, and Playwright MCP trace/evidence.

## Progression Gate

- Do not proceed to the next subbundle until all acceptance checks pass or a blocker is documented with a safe rollback/repair plan.

## Suggested Agent Prompt

Use the shared implementation prompt, then execute this subbundle only. Read the exact source references first, implement the deliverables, run the required tests, and update the execution report with proof before moving on.
