# 04-tab-by-tab-desktop-layout-pass

## Status

- `Completed`

## Completion Evidence

- All Cognitive Memory module tabs were reviewed for large-desktop scanning and updated where the changed data contract or quality operations affected the UI.
- Long-list tabs now have visible pagers backed by service-level page requests.
- Source scan found no `ColumnTemplateLg`, no Cognitive Memory page CSS `@media`, and no page-level `.Take(...)` calls.

## Objective

Improve every Cognitive Memory module tab for a large-screen desktop operator workflow with consistent counts, pagers, and dense panes.

## Covered Inputs

- UI-02, UI-03, UI-10, UI-11, UI-12, UI-13.

## Prerequisites

- Subbundles 01 through 03 complete.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryDashboardTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryProbeWorkbenchTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemorySettingsTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemorySourcesTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryMemoryTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryReviewQueueTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryRecallTracesTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryHealthTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemorySelfRegulationTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryScaleTab.razor`

## Deliverables

- Each tab has explicit large-screen layout and pager where lists exist.
- Root summary and tab badges show meaningful totals.
- Cognitive Memory CSS no longer contains medium/small media-query tuning.
- Existing BaseLib components remain the primary UI building blocks.

## Dependency Impact

- Subbundle 05 browser proof depends on all tabs being updated.

## Validation Depth

- Process-critical UI pass.

## Implementation Steps

1. Add consistent pager/header fragments.
2. Update each tab component.
3. Replace `ColumnTemplateLg` usage with large-screen-oriented templates where relevant.
4. Remove Cognitive Memory medium/small media queries.
5. Update component tests for all tabs.

## Do Not Do

- Do not optimize or tune medium or small screens.
- Do not add raw layout wrappers when an existing BaseLib wrapper already works.
- Do not add Radzen to this page.

## Acceptance Checklist

- Dashboard, Probe workbench, Quality operations, Settings, Sources, Memory, Review queue, Recall traces, Health, Self-regulation, and Scale are all improved.
- Every long-list panel has a pager.
- No Cognitive Memory page CSS media query remains for medium/small tuning.

## Proof Required

- Component tests for Cognitive Memory page.
- Source review for forbidden media queries.

## Browser Validation Logging

- Record a large desktop tab walk across every tab.

## Progression Gate

- Subbundle 05 may close only after all tabs are updated.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Improve every Cognitive Memory tab for large desktop and add visible pagers without medium/small tuning.
```
