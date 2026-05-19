# 02 - Multi-Key Clustering Foundation

## Status

- Status: `Completed`

## Objective

Add durable multi-key clustering support so memories/source items can be grouped by more than title or source identity. This creates the substrate for real dreaming.

## Covered Inputs

- User concern about clustering according to different keys.
- Current source-derived topic keys and generic claims.
- Existing relation, source, evidence, and memory record foundations.

## Prerequisites

- Subbundle 01 completed or its baseline findings accepted.
- EF persistence model conventions reviewed.
- Existing relation and claim records understood before new schema is added.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Foundation\CognitiveMemoryEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Neuro\CognitiveMemoryNeuroFoundationEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationCandidateApplicator.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallEvaluation.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallChannels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryPersistenceModelTests.cs

## Deliverables

- Cluster key model covering project/workspace, source topology, semantic topic, entity, task/intent, temporal, evidence overlap, relation/contradiction, and access/risk families.
- Durable cluster records, cluster key records, and cluster member records or an equivalent relation-based design.
- Cluster planner service with deterministic key extraction and stable cluster IDs.
- EF configuration and indexes for cluster lookup and membership updates.
- Unit and persistence tests for key computation, duplicate clustering, project isolation, and redaction-aware cluster membership.

## Dependency Impact

- Subbundle 03 depends on cluster records and planner outputs.
- Subbundle 04 depends on cluster members to build aggregate candidates.
- Subbundle 07 depends on cluster metrics and test corpus assertions.

## Validation Depth

- Tests must cover at least five key families, including semantic/title, source topology, project scope, temporal, and evidence overlap.
- Tests must show same-topic memories from different source items can join the same cluster without crossing project boundaries.
- Tests must show restricted/redacted source membership does not authorize unrestricted aggregation output.

## Implementation Steps

1. Design cluster DTOs and records consistent with current naming conventions.
2. Add cluster key extraction from memory records, source links, source items, evidence anchors, and metadata.
3. Add deterministic stable cluster IDs or stable unique constraints.
4. Add a cluster planner service with no generated synthesis yet.
5. Add persistence tests and unit tests.
6. Update docs and execution report.

## Scope Exceptions

- Clustering should not yet activate aggregate memories.
- Clustering may compute vector/semantic keys when available, but tests must pass with deterministic fakes.

## Do Not Do

- Do not cluster across projects unless an explicit cross-project mode and policy allows it.
- Do not ignore access/redaction state when forming aggregate-ready clusters.
- Do not rely solely on raw title equality.

## Acceptance Checklist

- [x] Cluster records or equivalent relation model exists.
- [x] Multiple key families are computed and tested.
- [x] Project isolation is enforced.
- [x] Cluster membership persistence is covered by tests.
- [x] Existing recall/consolidation tests remain green or are safely repaired.

## Proof Required

- Unit test output for cluster planner.
- EF model/persistence test output.
- Example cluster plan JSON or test snapshot showing key families and members.


## Browser Validation Logging

- Record browser validation only when this subbundle changes UI-rendered behavior.
- If no UI changes are made, record `Not applicable - API/domain-only change` in the execution report.
- If UI changes are made, capture route, viewport, screenshots, console errors, and Playwright MCP trace/evidence.

## Progression Gate

- Do not proceed to the next subbundle until all acceptance checks pass or a blocker is documented with a safe rollback/repair plan.

## Suggested Agent Prompt

Use the shared implementation prompt, then execute this subbundle only. Read the exact source references first, implement the deliverables, run the required tests, and update the execution report with proof before moving on.
