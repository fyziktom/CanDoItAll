# Subbundle 03 - Live Projection Rebuild Validation

## Status

- `Completed`

## Objective

- Execute a live projection rebuild against the running application and Docker Qdrant.
- Prove the rebuild writes projection data that Qdrant can serve.

## Covered Inputs

- CM-BETA-003: validate live projection rebuild against Docker Qdrant.
- CM-BETA-001: close the remaining P1 beta blocker if live projection proof succeeds.

## Prerequisites

- Subbundle 01 gate audit is completed.
- Subbundle 02 Docker/app runtime validation is completed.
- A durable Cognitive Memory source exists or is created through public app/API flows.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Operations\CognitiveMemoryProjectionRebuildService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Projection\CognitiveMemoryProjectionAdapters.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Projection\CognitiveMemoryProjectionAdapterContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.OperationsEndpoints.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.IngestionEndpoints.cs`
- `C:\repositories\CanDoItAll\docs\cognitive-memory\operations\provider-failure-runbook.md`

## Deliverables

- Live projection rebuild API proof.
- Qdrant collection point-count or point-scroll proof after rebuild.
- Any required narrow fixes for rebuild/provider validation, with tests.

## Dependency Impact

- May touch projection rebuild, projection adapters, ingestion orchestration, or API DTO mapping if runtime proof exposes a defect.
- Must not bypass application services with direct database writes.

## Validation Depth

- Execute against the running web app and Docker Qdrant.
- Confirm selected/rebuilt counts and provider warnings.
- Inspect Qdrant collection state after rebuild.

## Implementation Steps

1. Determine the smallest public API path to create or reuse durable Cognitive Memory projection input.
2. Run the projection rebuild endpoint or equivalent app API operation.
3. Query Qdrant collection metadata and points after rebuild.
4. Fix implementation defects only if proof exposes them.
5. Rerun targeted tests after any fix.
6. Record command/API proof in the execution report.

## Do Not Do

- Do not insert projection records directly into the database.
- Do not mark success when rebuild selected zero items without explaining why.
- Do not mark success if Qdrant remains empty after a rebuild that should write vectors.

## Acceptance Checklist

- Rebuild operation completes without provider failure.
- Rebuild result has concrete selected/rebuilt/skipped/error counts.
- Qdrant has the configured collection and at least one expected point when input exists.

## Proof Required

- API response summary with operation status and counts.
- Qdrant collection/point proof.
- Test/build output after any code fix.

## Browser Validation Logging

- Browser proof is optional unless UI controls are used to trigger rebuild.
- If UI is used, capture desktop and mobile proof under `reviews/browser-proof`.

## Progression Gate

- Continue to recall proof only when Qdrant contains projection data or a documented beta blocker explains why it cannot.

## Suggested Agent Prompt

```text
Execute a live projection rebuild through public Cognitive Memory app/API flows against Docker Qdrant. Prove the configured Qdrant collection receives projection data, or fix the smallest implementation defect that prevents it.
```
