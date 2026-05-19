# 01-cluster-search-data-contract-ui

## Status

- `Completed`

## Objective

Add a large-screen Cognitive Memory cluster-search tab backed by server-side search, filters, paging, and bounded previews.

## Covered Inputs

- REQ-01, REQ-02, REQ-03, REQ-04.

## Prerequisites

- Prior UI quality operations work exists.
- Review UI service is the correct UI data boundary.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.Quality.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiQualityQueries.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryReviewUiServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CognitiveMemoryPageTests.cs`

## Deliverables

- Strongly typed cluster-search query and result contracts.
- Review UI query methods with server-side filtering and paging.
- `Cluster Search` tab/component on the Cognitive Memory page.
- Unit and component tests covering access, filtering, and paging behavior.

## Dependency Impact

- Browser proof depends on this tab existing.
- Realistic validation depends on an operator-visible way to inspect cluster outputs.

## Validation Depth

- Code-critical and UI-critical.

## Implementation Steps

1. Extend Review UI contracts for cluster-search filters/results.
2. Add query methods that filter cluster records and keys server-side.
3. Add page state and tab/component wiring.
4. Add tests for query behavior and component visibility.
5. Run focused tests and build.

## Do Not Do

- Do not load all clusters, keys, members, memories, or sources.
- Do not add medium/small-screen styling.
- Do not bypass the Review UI service from the component.

## Acceptance Checklist

- Cluster search tab is reachable.
- Search filters reset result paging.
- Result totals are filtered and paged.
- Key/member previews are bounded to the current page.
- Tests and build pass.

## Proof Required

- Focused unit test output.
- Focused component test output.
- Web build output.
- Large-screen browser screenshot.

## Browser Validation Logging

- Route: `/cognitive-memory`
- Viewport: `1920x1080`
- Actions: open Cluster Search tab, apply search/filter, page results.
- Evidence: screenshot under `proof/browser`.

## Progression Gate

- Subbundle 02 may run in parallel after code implementation starts, but subbundle 05 cannot claim cluster validation until this subbundle is completed.

## Suggested Agent Prompt

```text
Implement subbundle 01. Add a large-screen Cognitive Memory Cluster Search tab backed by server-side Review UI service paging and filtering. Update focused tests and record proof.
```
