# 02-cluster-planner-idempotency-and-source-substrate

## Status

- `Completed`

## Objective

Fix the cluster planner so it produces durable, repeatable cluster plans and makes the source-item member contract honest.

## Success Criteria

- Re-running `CognitiveMemoryClusterPlanner.PlanAsync` for the same scope returns the same persisted cluster IDs for existing hashes.
- A second dream run with a different idempotency key can use existing clusters without FK failures or transient cluster IDs.
- Cluster keys, members, counters, readiness, and warnings are updated or preserved by an explicit rule.
- `CognitiveMemoryQualityClusterMemberKind.SourceItem` is implemented with tests or explicitly removed/narrowed from reachable behavior with a documented exception.

## Covered Inputs

- H-03, H-04, H-15.

## Prerequisites

- Subbundle 01 entry and closure gates have passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryClusterPlanner.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualitySupport.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryQualityPersistenceModelTests.cs`

## Deliverables

- Cluster upsert/reuse logic that hydrates existing cluster IDs into returned plans.
- Tests for repeated planner calls and second dream-run use of existing clusters.
- Source-item member implementation or explicit contract narrowing.
- Metrics that distinguish clusters found from clusters inserted/updated when useful.

## Dependency Impact

- Dream runs, aggregate candidates, validations, and end-to-end corpus proof depend on stable cluster identity. Weak proof here invalidates Subbundles 03, 04, and 07.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Use the tests from Subbundle 01 to reproduce the current repeat-planning defect.
2. Refactor cluster persistence to upsert or reuse clusters by `(ProjectId, ClusterHash)`.
3. Ensure returned `CognitiveMemoryClusterPlan.ClusterId` always matches the durable cluster row when persistence is enabled.
4. Decide the source-item member model and implement or explicitly narrow it.
5. Add integration tests that enforce FK integrity when a second dream run uses existing clusters.
6. Update metrics and warnings only where they improve operational clarity.

## Scope Exceptions

- Do not implement semantic/vector clustering in this subbundle unless needed to make existing key families correct.

## Do Not Do

- Do not change dream-run lifecycle or mode-policy behavior here except as needed to prove cluster reuse.
- Do not suppress FK errors or catch-and-ignore persistence exceptions.

## Acceptance Checklist

- Repeat planner test passes.
- Second dream-run existing-cluster test passes.
- Source-item member contract is implemented or narrowed with a clear note.
- Cluster metrics no longer overstate inserted clusters as created on every run.

## Proof Required

- Targeted unit tests for cluster planner behavior.
- Targeted integration test proving existing cluster IDs are reused by a second dream run.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityFoundationTests" --logger "console;verbosity=minimal" -m:1`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityPersistenceModelTests" --logger "console;verbosity=minimal" -m:1`

## Browser Validation Logging

- N/A. This subbundle is API/persistence behavior only.

## Progression Gate

- Subbundle 03 may start only after repeat cluster planning and second dream-run FK integrity are proven.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Fix cluster planner durability and source/member honesty. Keep changes scoped to cluster planning, related persistence contracts, and tests. Record exact proof in the execution report.
```
