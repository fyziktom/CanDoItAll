# 02-paged-review-ui-data-contract

## Status

- `Completed`

## Completion Evidence

- `CognitiveMemoryReviewUiQuery` now carries per-collection page requests.
- `CognitiveMemoryReviewUiSnapshot` now returns paging metadata and bounded quality-operation lists.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter CognitiveMemoryReviewUiServiceTests --no-restore` passed.

## Objective

Add per-collection paging to the Cognitive Memory review UI service and stop loading all rows before taking a page.

## Covered Inputs

- UI-03, UI-04, UI-05, UI-07, UI-10.

## Prerequisites

- Subbundle 01 complete.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiSummaryQueries.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiTraceHealthQueries.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiAdvancedQueries.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiAuditQueries.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryReviewUiServiceTests.cs`

## Deliverables

- Per-collection page request and page metadata contracts.
- Quality list view contracts for clusters, dream runs, aggregate candidates, and synthesized recalls.
- Query methods apply `OrderBy`, `Skip`, and `Take` before materializing rows where possible.
- Unit tests prove page metadata and page-window behavior.

## Dependency Impact

- Subbundles 03 and 04 depend on this metadata for visible pagers and quality lists.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Extend review UI contracts.
2. Add service helpers for resolving page windows.
3. Update existing list queries to use page windows.
4. Add quality query loaders.
5. Add unit tests.

## Do Not Do

- Do not fake paging only in the UI.
- Do not use `ToListAsync` before `Skip`/`Take` for normal list queries.
- Do not add a new API layer if the existing review UI service boundary can carry the data.

## Acceptance Checklist

- Unit tests prove non-default page request behavior.
- Snapshot has page metadata for all long-list collections.
- Quality lists are included and bounded.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryReviewUiServiceTests" --logger "console;verbosity=minimal" -m:1`

## Browser Validation Logging

- N/A for this data-contract subbundle.

## Progression Gate

- Subbundle 03 may proceed only after page metadata and quality lists are available.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Add per-collection paging and quality UI list contracts. Prove the service does not depend on loading full lists for page results.
```
