# 05-crmhr-operations-tabs-and-dialogs

## Status

- `Completed`

## Objective

- Redesign supporting CRM/HR and operations tab/dialog-heavy content after the shared tree/detail, dense tab, and inspector dialog patterns are available.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-007 use maximum desktop width.
- RN-008 design proposals for tab contents and dialogs.
- RN-009 use BaseLib/Tailwind/shared component mechanisms.
- RN-010 use dialogs for too much information.
- RN-012 professional B2B video readiness.

## Prerequisites

- SB00-01 page inputs and proposals passed.
- SB00-03 reusable tree/detail/tab/dialog primitives passed.
- SB05 supporting module density pass has preserved route-level behavior or this subbundle owns the related changes directly.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\05-crm-hr.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\06-operations-supporting.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\06-supporting-pages-tabs-dialogs-proposal.png`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\PartyMergeDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\OpportunityConversionDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Collaboration\Pages\CollaborationHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\Pages\SchedulerPlannerPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor`

## Deliverables

- CRM/HR directory, CRM, workforce, recruiting, agents, and assignments use compact list/detail or tree/detail surfaces.
- Party merge and opportunity conversion dialogs use inspector dialog patterns where touched.
- Collaboration `Inbox`, `Threads`, and `Escalations` tabs use a three-pane work queue layout.
- Scheduler `Scheduled runs`, `New schedule`, `History` tabs and target picker dialog use dense tab/dialog patterns.
- Validation and Test Lab detail sections are reorganized into compact operational panels or tabs.
- Activity and Automation use dense timeline/job board layouts.

## Dependency Impact

- SB06 depends on these routes for complete visual coverage and raw note closure.
- CRM/HR tests may need updates if list/detail selection and dialogs move.

## Validation Depth

- UI browser proof with targeted regression tests for moved CRM/HR and operations flows.

## Implementation Steps

1. Work route-by-route from page inputs.
2. Preserve all create/save/open/reset/convert/merge/reply/search/schedule/run flows.
3. Use tree/list/detail where hierarchy or grouping exists; use compact lists where no hierarchy exists.
4. Move secondary details into dialog/inspector panels.
5. Use shared BaseLib variants and Tailwind utility composition only.
6. Add or update tests for moved dialogs and tab interactions.
7. Capture large-screen screenshots for route states and open dialogs.

## Scope Exceptions

- Do not deeply refactor CRM/HR domain services.
- Do not force tree views onto flat data with no useful grouping.
- Do not tune mobile/medium.

## Do Not Do

- Do not hide critical warning/review/approval actions.
- Do not add new page-local CSS.
- Do not convert operational pages into marketing hubs.

## Acceptance Checklist

- CRM/HR dense pages preserve all listed form sections and actions.
- Collaboration, scheduler, validation, and test lab tab/dialog states are covered.
- Dialogs are readable and unclipped on large desktop.
- Existing smoke flows pass or are intentionally updated.
- No new page-local CSS is added.

## Proof Required

- Targeted CRM/HR and operations Playwright/component tests.
- Large-screen screenshots for each changed route and open dialog.
- Execution report rows updated with tab/dialog proof.
- Diff review for no page-local CSS.

## Browser Validation Logging

- Routes: `/crm-hr`, `/crm-hr/directory`, `/crm-hr/crm`, `/crm-hr/workforce`, `/crm-hr/recruiting`, `/crm-hr/agents`, `/crm-hr/assignments`, `/collaboration`, `/activity`, `/automation`, `/scheduler`, `/validation`, `/test-lab`.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: select list/tree rows, switch tabs, open merge/conversion/target picker/detail dialogs, save/cancel representative forms, search/filter.
- Screenshots: route after states, tab bodies, and dialog open states.
- Review questions: are workflows understandable, are dense details reachable, does the page look professional, and does it use desktop width well.

## Progression Gate

- SB06 may close supporting routes only after all changed CRM/HR and operations tab/dialog states have proof or explicit blockers.

## Suggested Agent Prompt

```text
Implement subbundle 05-06 only. Redesign CRM/HR and supporting operations tab/dialog-heavy content using shared tree/detail, dense tab, and inspector dialog patterns. Preserve every existing flow, avoid page-local CSS, run targeted tests, capture large-screen screenshots, and update the execution report.
```
