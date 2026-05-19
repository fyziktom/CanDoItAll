# 04-project-source-truth-transfer-and-ingestion

## Status

- `Completed`

## Objective

Transfer or stage project/project-structure/files/data source truth into the validation profile and ingest it through supported Cognitive Memory APIs.

## Covered Inputs

- REQ-07, REQ-08, REQ-09.

## Prerequisites

- Subbundle 03 completed or blocked with a partial-validation path.
- Workbook exists.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseTransferModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\DatabaseTransfer\ProjectsDatabaseTransferHandler.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.IngestionEndpoints.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProjectsApi.cs`

## Deliverables

- Transfer path assessment.
- Project/source inventory for ingestion.
- Ingestion API calls or explicit unsupported-transfer blocker.
- Source truth comparison notes.

## Dependency Impact

- Clustering, dreaming, and probe validation need a meaningful source corpus.

## Validation Depth

- Behavior-critical.

## Implementation Steps

1. Inspect supported database transfer handlers for projects and structures.
2. Determine whether a public app path can transfer source truth to the clean profile.
3. Use project-structure and external-source ingestion APIs for supported source truth.
4. Record counts and mismatches in the workbook.

## Do Not Do

- Do not manually copy Cognitive Memory facts into a clean profile.
- Do not read or upload sensitive excluded files.
- Do not treat transfer settings as project-data transfer unless verified.

## Acceptance Checklist

- Transfer capability is proven or blocked.
- Ingestion source set is documented.
- Ingestion results are recorded.
- Source truth comparison is started.

## Proof Required

- API operation IDs and result captures.
- Workbook rows updated.
- Trouble rows for unsupported paths.

## Browser Validation Logging

- N/A unless source selection UI is inspected.

## Progression Gate

- Subbundle 05 can run full validation only after at least one meaningful source-truth ingestion completes.

## Suggested Agent Prompt

```text
Execute subbundle 04. Discover and use supported project/project-structure transfer and Cognitive Memory ingestion paths. Record operation IDs, counts, and blockers.
```
