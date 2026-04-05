# 01 - Canonical node assignment owner and editor read path

## Status

- `Completed`

## Objective

- Establish `ProjectPartyAssignment` as the canonical owner for node-scoped participant, meeting, and work-item party links and make the structure-page editor read from that canonical source.

## Covered Inputs

- `RQ-01 Canonical Owner`
- `RQ-02 Canonical Read Path`
- `RQ-03 Derived Projection Only`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectPartyIntegrationContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.PartyIntegration.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePartyPickerTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\ProjectPartyAssignmentFlowTests.cs`

## Deliverables

- Bridge contract supports canonical node-assignment replacement in one operation.
- Structure-page participant, meeting, and work-item editors initialize from assignment rows.
- Metadata remains a derived projection instead of the editor truth source.

## Dependency Impact

- `02` depends on this phase because lifecycle reconciliation must target the same canonical storage contract.
- `03` depends on this phase because browser proof is invalid if the editor still reads stale metadata.

## Validation Depth

- Critical integration foundation

## Implementation Steps

1. Extend the project-facing bridge with a canonical node-assignment replacement operation.
2. Implement that operation in CRM/HR with assignment validation and one-save persistence.
3. Update the structure-page editor to read selected state from canonical assignments.
4. Keep metadata updates as derived projection writes only.
5. Update component coverage if the new canonical read path changes expectations.

## Scope Exceptions

- This phase does not solve the broader `NodeKey` type-system weakness.

## Do Not Do

- Do not introduce direct CRM/HR persistence type dependencies into Workbench.
- Do not widen this phase into delete or subtree-transfer lifecycle repair.

## Acceptance Checklist

- Participant editor selection reflects canonical assignment rows.
- Meeting editor selection reflects canonical meeting-participant assignment rows.
- Work-item editor selection reflects canonical assignee rows.
- Saving through the page still updates preview metadata without making metadata the editor truth source.

## Proof Required

- Run the smallest build slice that compiles the touched contracts.
- Run component tests for the structure-page party editor.
- Run at least the affected Playwright structure-page assignment flow if this phase changes browser-visible behavior.

## Browser Validation Logging

- Target route: `/projects/{ProjectId}/structure`.
- Required viewport passes: `1600x1000`, then narrower follow-up if layout changes.
- Required Playwright MCP evidence: select participant, meeting, and work-item nodes and confirm the editor state reflects assignment-backed values.
- Expected screenshots: structure-page evidence captured again during closure or deferred into subbundle `03` if this phase stays green and unchanged visually.
- Screenshot review questions: does the correct selected party state appear immediately after node selection, and is there any stale editor state during selection changes?

## Progression Gate

- `02` may start only after the canonical bridge operation exists and the editor no longer depends on metadata for its initial selected state.

## Suggested Agent Prompt

```text
Implement this subbundle only. Keep Workbench on project-facing bridge contracts and make the structure-page party editor read from canonical assignments instead of metadata.
```
