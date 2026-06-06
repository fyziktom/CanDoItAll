# Current-state review

## Completed from the previous bundle

The latest branch shows a successful artifact projection coordinator split:

- `ProcessArtifactProjectionOrchestrator` owns the source-family ordering.
- `ProjectExecutionArtifactsAsync` delegates to orchestrator instead of containing all source-family bodies.
- Source scans from the previous bundle report no Core, driver API, UI, or viewport proof drift.

## Remaining architectural gap

`IProcessArtifactProjectionHost` is now a broad adapter. It contains path resolution, claim guard, artifact matching, classification, session observations, project-structure matching, response rules, browser rules, decision rules and lineage helpers. That is better than passing the whole dispatcher into coordinators, but still too broad for Core readiness.

`DispatcherArtifactProjectionHost` forwards many calls back into `ProcessRunAutomationDispatchService`. This is acceptable as a transitional bridge, but it must not become the next stable architecture.

## Recommendation

Do not start Process Core yet. First split the projection host into narrow module-local facets and update coordinators to depend only on the facets they actually need.
