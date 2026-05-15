# 05-supporting-module-page-density-pass

## Status

- `Ready`

## Objective

- Apply the same large-screen visual clarity rules to supporting modules: CRM/HR, collaboration, activity, automation, scheduler, validation, and test lab.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-007 use maximum available width.
- RN-008 analyze page screenshots and repair until visually aligned.
- RN-009 no own CSS; use Tailwind/BaseLib/component options.
- RN-010 use dialogs for too much information.
- RN-012 professional B2B customer-video readiness.

## Prerequisites

- SB00-03 reusable layout/tab/dialog primitives passed.
- SB01 route baseline and proposals exist for each owned route.
- SB02 shell foundation passed.
- Any shared pattern from SB04 that should apply globally has been identified.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAgentsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAssignmentsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Collaboration\Pages\CollaborationHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\Pages\ActivityPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\Pages\SchedulerPlannerPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CrmHrShellSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CrmHrCrossModuleFlowTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\StaffingFlowTests.cs`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\05-crm-hr.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\06-operations-supporting.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\06-supporting-pages-tabs-dialogs-proposal.png`

## Deliverables

- Supporting module pages use the compact shell and full-width scaffold rules.
- CRM/HR suite avoids long page-spanning card stacks where list/detail or dialogs are clearer.
- Collaboration/activity/automation pages use denser operational list/table/timeline layouts.
- Scheduler/validation/test-lab pages retain required detail but reduce redundant header text and unused gutters.
- Deep tab/dialog work for CRM/HR and operations routes is coordinated with subbundle `05-06-crmhr-operations-tabs-and-dialogs`.
- Route inventory updated with after screenshots and exceptions.

## Dependency Impact

- SB06 depends on this phase for final route coverage and raw note closure.
- Shared improvements discovered here may require reopening SB04 or SB02 if they reveal a shell/component foundation gap.

## Validation Depth

- UI browser-proof with targeted regression tests for changed flows.

## Implementation Steps

1. Work page-by-page from the SB01 inventory and avoid broad rewrites.
2. Apply full-width `PageScaffold` and shared layout primitives where the page currently wastes desktop space.
3. Convert dense repeated information to list/detail, tabs, dialogs, or compact summary rows.
4. Preserve existing CRM/HR, collaboration, scheduler, validation, and automation flows.
5. Add or update targeted tests only where interactions move.
6. Capture large-screen after screenshots and update the execution report.

## Scope Exceptions

- Do not introduce tree views where the module has no hierarchy or grouping value; record a compact-list exception instead.
- Do not duplicate tab/dialog-specific work owned by subbundle `05-06-crmhr-operations-tabs-and-dialogs`; either complete it there or record the handoff.
- Do not deeply refactor CRM/HR domain behavior while doing visual tuning.

## Do Not Do

- Do not add new page-local custom CSS.
- Do not convert B2B operational screens into landing pages.
- Do not hide critical statuses, warnings, or approval actions behind non-obvious controls.
- Do not tune mobile/tablet breakpoints.

## Acceptance Checklist

- Every owned route has a large-screen after screenshot or explicit low-change exception.
- Major dense pages have less visual clutter and clearer primary workspace.
- Details remain reachable through tabs/dialogs/flyouts.
- Existing Playwright smoke flows still pass or are updated for intentional UI movement.
- No new page-local CSS was added.

## Proof Required

- Targeted Playwright or component tests for changed supporting-module interactions.
- Large-screen after screenshots for every owned route.
- Open-state screenshots for new/changed dialogs.
- Execution report route rows and raw-note status updates.

## Browser Validation Logging

- Routes: `/crm-hr`, `/crm-hr/directory`, `/crm-hr/crm`, `/crm-hr/workforce`, `/crm-hr/recruiting`, `/crm-hr/agents`, `/crm-hr/assignments`, `/collaboration`, `/activity`, `/automation`, `/scheduler`, `/validation`, `/test-lab`.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: navigate, exercise primary tab/filter/list interactions, open detail dialogs, verify primary action reachability.
- Screenshots: after screenshot for each route and open-state screenshots for changed dialogs.
- Review questions: does the page read as professional B2B software, does it use width well, is the main action clear, and are secondary details reachable without clutter.

## Progression Gate

- SB06 may start final closure only after every supporting route has screenshot proof or an explicit exception row in the execution report.

## Suggested Agent Prompt

```text
Implement subbundle 05 only. Apply the large-screen visual refresh to CRM/HR, collaboration, activity, automation, scheduler, validation, and test lab pages. Keep domain behavior intact, reduce visual clutter, use dialogs for secondary details, avoid new custom CSS, capture route screenshots, and update the execution report.
```
