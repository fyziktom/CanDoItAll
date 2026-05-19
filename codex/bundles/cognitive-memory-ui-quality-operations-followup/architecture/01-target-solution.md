# Target Solution

## UI Shape

- Keep `/cognitive-memory` as a large desktop operator workspace.
- Add a `Quality operations` tab near the Dashboard/Probe tabs.
- Use existing BaseLib wrappers: `Tabs`, `Grid`, `Stack`, `Cluster`, `Split`, `SurfaceCard`, `SummaryTile`, `SelectionListItem`, `StatusBadge`, `Button`, and `EmptyState`.
- Use `ColumnTemplateXl` for desktop composition and remove Cognitive Memory page media queries.
- Use compact pane headers with total counts and explicit previous/next page controls.

## Data Contract

- Extend `CognitiveMemoryReviewUiQuery` with per-collection page requests.
- Add page metadata to the snapshot.
- Add paged quality views for clusters, dream runs, aggregate candidates, and synthesized recalls.
- Apply `OrderBy`/`Skip`/`Take` before materializing rows.
- Keep bounded child collections for detail rows, such as source links or recall candidates.

## Quality Operations

- Diagnostics action calls `ICognitiveMemoryQualityDiagnosticsService`.
- Cluster planning action calls `ICognitiveMemoryClusterPlanner`.
- Dream action calls `ICognitiveMemoryDreamConsolidationService` and must show whether it persisted.
- Approved aggregate candidate apply action calls `ICognitiveMemoryAggregateMemoryApplicator`.
- Actions use explicit status text and notification outcomes.

## Validation Strategy

- Unit tests prove review UI paging and quality lists.
- Component tests prove tab access, pager controls, quality actions, and large-screen-only markup decisions.
- Build the CognitiveMemory module.
- Run browser proof on `/cognitive-memory` at a large desktop viewport, walking every tab.
