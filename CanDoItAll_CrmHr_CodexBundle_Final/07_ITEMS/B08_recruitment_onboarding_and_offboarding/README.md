# B08 — Recruitment pipeline, interviews, onboarding, and offboarding

## Status

- `Completed on 2026-04-03`

## Purpose

Implement candidate handling, interview scheduling, structured feedback, hiring conversion, onboarding and offboarding task management, and lifecycle reminders.

## Dependencies

B01, B02, B03, B06

## Main stories covered

- **HR-19** As a recruiter, I can create a candidate record in the same unified registry so future employees and contractors do not start in a disconnected tool.
- **HR-20** As a recruiter, I can track candidate stage from sourced to hired or rejected so recruitment progress is visible.
- **HR-21** As a recruiter, I can schedule interviews and record interview dates so hiring coordination is structured.
- **HR-22** As a hiring manager, I can capture interview feedback and recommendation so decision quality is documented.
- **HR-23** As a people ops manager, I can convert a hired candidate into an employee or contractor profile so recruiting handoff is seamless.
- **HR-24** As a people ops manager, I can create onboarding tasks with owner and due date so new joiners do not rely on ad hoc follow-up.
- **HR-25** As a people ops manager, I can create offboarding tasks with owner and due date so exits are controlled.
- **HR-26** As a mentor coordinator, I can assign manager, buddy, or mentor relationships for onboarding so support roles are visible.
- **HR-27** As an IT coordinator, I can track access and equipment checklist items during onboarding or offboarding so delivery readiness is observable.
- **X-15** As an automation owner, I can trigger reminders and onboarding follow-up jobs from CRM/HR data so the module participates in operational automation.

## Main routes

- `/crm-hr/recruiting`

## Done when

- Candidates move through recruitment stages with history preserved.
- Interviews and feedback persist.
- Hiring conversion can create workforce identity without duplicating the person.
- Onboarding/offboarding tasks are visible and actionable.

## Execution Notes

- `/crm-hr/recruiting` now owns candidate quick-create/edit, stage transitions with preserved history, structured interview scheduling and feedback, support-role assignment, and lifecycle task management for onboarding and offboarding follow-up.
- Hiring conversion now reuses the live workforce path instead of introducing a second identity flow. The candidate stays on the same party, conversion writes the workforce profile through the existing HR service, and manager, buddy, and mentor assignments map onto the current party-relationship model.
- Closure repaired stale bundle assumptions against the current repo. Stage history is backed by `CrmHrAuditEntry`, visible recruiting actions write through the shared `IActivityStream`, project-linked lifecycle tasks use the current project option queries, and SQLite validation required client-side ordering for interview and audit timelines that include `DateTimeOffset`.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CanDoItAll.Modules.CrmHr.csproj -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter RecruitingPageTests -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter RecruitmentLifecycleIntegrationTests -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter RecruitmentFlowTests -v minimal`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b08\crm-hr-recruiting-b08-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b08\crm-hr-recruiting-b08-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b08\screenshot-review.md`
