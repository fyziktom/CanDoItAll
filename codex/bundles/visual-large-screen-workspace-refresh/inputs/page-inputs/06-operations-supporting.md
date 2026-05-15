# Page Inputs: Supporting Operations Pages

## PI-COLLABORATION Collaboration `/collaboration`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Collaboration\Pages\CollaborationHomePage.razor`

Current display:
- `PageScaffold` with tabs `Inbox`, `Threads`, and `Escalations`.
- Summary tiles `Threads`, `Unread`, `Escalations`, and `Transport boundary`.
- Actions include `Create collaboration item`, `Add reply`, `Mark selected read`, `Notification`, `Escalation`, `Open activity`, `Open linked context`, `Show all`, `Unread only`, `Clear`, and `Reset`.

Current UX flows:
- User filters inbox/thread/escalation items, selects conversation, replies, marks read, opens linked context or activity.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 6.
- Three-pane inbox/thread/context layout with compact tab body for each tab.

Function coverage confirmation:
- Covers all tabs and collaboration actions.
- Makes conversation flow clearer without turning it into a card stack.

## PI-ACTIVITY Activity `/activity`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\Pages\ActivityPage.razor`

Current display:
- Timeline/search page with empty states `No query yet`, `Nothing matched this query`, and `The timeline is empty`.
- Actions include `Search`, `Clear`, and `Open`.

Current UX flows:
- User searches activity, clears query, opens linked activity result.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 7.
- Full-width timeline search with compact filters and result rows.

Function coverage confirmation:
- Covers search, empty states, clear, and open.
- Improves scanability for customer presentation.

## PI-AUTOMATION Automation `/automation`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor`

Current display:
- Automation operations page with summary tiles `Jobs`, `Running`, `Succeeded`, `Failed`, and `CRM-HR signals`.
- Empty states for job filters and reminder work.
- Actions include `Open` and `Reset`.

Current UX flows:
- User scans background job state, opens job context, resets filters.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 7.
- Operational job board/table with compact status strip.

Function coverage confirmation:
- Covers status counts, job list, open and reset.
- Makes automation state easier to scan on large screen.

## PI-SCHEDULER Scheduler `/scheduler`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\Pages\SchedulerPlannerPage.razor`

Current display:
- Scheduler page with tabs `Scheduled runs`, `New schedule`, and `History`.
- Summary tiles `Schedules`, `Enabled`, `Next 30 days`, and `Failures`.
- Dialog `Choose workflow or process`.
- Actions include `New`, `Refresh`, `Reset`, `Save schedule`, `Search`, `Choose`, and per-plan enable/disable action.

Current UX flows:
- User reviews scheduled runs, creates schedule, chooses target workflow/process, saves schedule, reviews history.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 8.
- Scheduled runs/new schedule/history tabs as dense panels with target picker dialog.

Function coverage confirmation:
- Covers all scheduler tabs and target picker flow.
- Keeps schedule creation visible without cluttering the schedule table.

## PI-VALIDATION Validation Center `/validation`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor`

Current display:
- Validation page with summary tiles `Runs`, `Findings`, `Needs review`, and `Visible`.
- Form sections `Source content`, `Run setup`, `Findings`, and `Review decision`.
- Actions include `New validation`, `Run validation`, and `Reset`.

Current UX flows:
- User creates validation run, enters source/setup, runs validation, reviews findings, records decision.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 8.
- Validation list/detail with findings table and review panel.

Function coverage confirmation:
- Covers run setup, source content, findings, review decision, new/run/reset.
- Uses dense detail layout for findings.

## PI-TEST-LAB Test Lab `/test-lab`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor`

Current display:
- Test lab page with summary tiles `Plans`, `Cases`, `Evidence`, and `Visible`.
- Form sections `Plan overview`, `Test cases`, `Execution runs`, and `Evidence`.
- Actions include `New test plan`, `Save plan`, `Reset`, `Add case`, `Add run`, and `Add evidence`.

Current UX flows:
- User creates/edits test plan, adds test cases, records execution runs, attaches evidence.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 8.
- Test plan list/detail with case/run/evidence tabs or compact panels.

Function coverage confirmation:
- Covers plan CRUD, cases, runs, evidence, and empty states.
- Improves test management density for large screens.

## PI-ERRORS Error And Not Found

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\NotFound.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\Error.razor`

Current display:
- Minimal error/not-found pages.

Current UX flows:
- User lands on error/not-found state and should still have shell navigation to recover.

Target proposal:
- Keep minimal.
- Only verify shell does not make these states look broken.

Function coverage confirmation:
- Covered by final shell proof, no separate visual redesign needed unless runtime screenshots show awkward spacing.
