# 02 - Projection-only party metadata and display guardrails

## Status

- `Completed`

## Objective

- Remove canonical-looking party identifiers and rich linked-party payloads from Workbench metadata while preserving the display-side summaries the structure surface still needs.

## Covered Inputs

- `RQ-03`
- `RQ-04`

## Prerequisites

- `01-workbench-lifecycle-compensation-and-typed-node-reference` must be completed or honestly blocked before this phase starts.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.PartyIntegration.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePartyPickerTests.cs`

## Deliverables

- Projection-only meeting, participant, and work-item party summaries in metadata
- Updated structure-page save flow that writes only display-side metadata
- Component proof that canonical loading still works and the display summary still updates

## Dependency Impact

- `03` depends on this phase because the review and ADR closure should reflect the shipped metadata discipline, not only intent.

## Validation Depth

- Critical projection-discipline phase

## Implementation Steps

1. Remove participant and work-item party ids from metadata and narrow meeting linked-party projection to display summaries only.
2. Update the structure-page save flow and any descriptors that still need party display text.
3. Update component assertions so they verify projection behavior without treating metadata as canonical identity storage.

## Do Not Do

- Do not break existing structure preview/describer facts.
- Do not reintroduce metadata-backed editor loading.

## Acceptance Checklist

- Participant metadata no longer stores a central party id.
- Work-item metadata no longer stores a central assignee id.
- Meeting metadata no longer stores rich linked-party identity payload that duplicates canonical rows.
- Structure UI still shows the correct summary text after save.

## Proof Required

- Relevant build slice
- Component tests for participant, meeting, and work-item flows
- Dependent smoke in `03`

## Browser Validation Logging

- Target route: `/projects/{ProjectId}/structure`
- Required viewport passes: `1600x1000`, then narrower follow-up if layout changed
- Required Playwright MCP evidence: confirm participant, meeting, and work-item editor flows still show the right linked summary after save
- Expected screenshots: structure route screenshots captured in `03`

## Progression Gate

- `03` may start only after metadata is projection-only and component proof is green.

## Suggested Agent Prompt

```text
Implement this subbundle only. Reduce Workbench party metadata to display-only projection fields without regressing the structure-page editor or preview summaries.
```
