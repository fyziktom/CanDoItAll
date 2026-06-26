# Post-SB34 Process Estimate Historical Cost Semantic Invariants

- Process-start price estimates prefer historical actual cost only when completed runs for the same `ProcessDefinitionId` have resolvable actual usage cost.
- Historical cost is averaged per completed process run, not per usage observation, so runs with more internal observations do not dominate the estimate.
- A completed root process sample includes descendant run IDs so subprocess cost contributes to the top-level process estimate.
- Failed, cancelled, blocked, and active runs do not contribute historical estimate samples.
- Historical runs with no resolvable actual cost do not silently produce a zero-dollar historical estimate. The estimator falls back to provider model pricing and says historical completed runs lacked resolvable actual usage cost.
- The project-structure launch dialog keeps the existing strongly typed `ProjectStructureProcessEstimateSummary` UI contract; only the estimate source and cost calculation change.
- The historical-cost reader is a read-only query over existing process runtime state, process plans, and AgentFramework usage telemetry. No schema migration or new persisted process record is introduced.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProcessHistoricalRunCostEstimate` | `EfProcessHistoricalRunCostReader` | `ProjectStructureProcessStartEstimateCalculator` | Per-preview, read-only, not persisted. | `repo://tests/CanDoItAll.Tests.Unit/EfProcessHistoricalRunCostReaderTests.cs` covers no matching completed runs and ignored failed runs. |
| `ProjectStructureProcessEstimateSummary.SourceLabel = "Historical run costs"` | `ProjectStructureProcessStartEstimateCalculator` | HR/staffing dialog estimate UI | Rendered only when actual historical costs exist. | `repo://tests/CanDoItAll.Tests.Unit/ProjectStructureProcessStartEstimateCalculatorTests.cs` covers historical preference and provider fallback. |
