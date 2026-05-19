# Source Artifacts

| Artifact | Path | Reason |
|---|---|---|
| Cognitive Memory page | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor` | Root module page and tab registration. |
| Page code-behind | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs` | UI state, refresh, selections, and injected services. |
| Page rendering helpers | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.Rendering.cs` | Shared row/list rendering used by many tabs. |
| Page CSS | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.css` | Existing local styles and media queries. |
| Page tab components | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components` | Dashboard, probe, settings, sources, memory, review, traces, health, self-regulation, and scale tabs. |
| Review UI contracts | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiContracts.cs` | Snapshot contract that currently has one global take value and no per-list paging metadata. |
| Review UI service | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs` | Snapshot loader that orchestrates all tab datasets. |
| Review UI summary queries | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiSummaryQueries.cs` | Memory and review list queries currently affected by page sizing. |
| Review UI trace and health queries | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiTraceHealthQueries.cs` | Trace, consolidation, projection, procedure, and replay list queries. |
| Review UI advanced queries | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiAdvancedQueries.cs` | Probe, self-regulation, answer gate, professor, learning, cross-project, and distributed query surfaces. |
| Review UI audit queries | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiAuditQueries.cs` | Combined operator audit list; must stay bounded. |
| Quality contracts | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs` | New diagnostics, cluster, dream, aggregate, synthesis, and reference contracts. |
| Quality entities | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntities.cs` | Durable cluster, dream, aggregate, validation, and synthesized recall records to expose in UI. |
| Component tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CognitiveMemoryPageTests.cs` | Existing component-level proof for the page tabs. |
| Review UI service tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryReviewUiServiceTests.cs` | Existing unit proof for snapshot data. |
| Image proposal overview | `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-ui-quality-operations-followup\inputs\imagegen\proposal-overview.png` | Generated planning artifact for desktop operator UI direction. |
| Image proposal tabs | `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-ui-quality-operations-followup\inputs\imagegen\proposal-tabs.png` | Generated planning artifact for tab-by-tab desktop layout direction. |

## Component MCP Note

The CanDoItAll components MCP was queried before markup edits, but the MCP transport closed. Implementation must therefore rely on existing BaseLib components already used by this page: `PageScaffold`, `PageHeader`, `Tabs`, `Grid`, `Stack`, `Cluster`, `Split`, `SurfaceCard`, `SummaryTile`, `SelectionListItem`, `StatusBadge`, `EmptyState`, and `Button`.
