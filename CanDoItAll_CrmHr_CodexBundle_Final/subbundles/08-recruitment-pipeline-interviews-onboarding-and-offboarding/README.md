# B08 - Recruitment pipeline, interviews, onboarding, and offboarding

## Status

- `Completed`

## Objective

- Implement candidate handling, interview scheduling, structured feedback, hiring conversion, onboarding and offboarding task management, and lifecycle reminders.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B08_recruitment_onboarding_and_offboarding`
- Story IDs: HR-19, HR-20, HR-21, HR-22, HR-23, HR-24, HR-25, HR-26, HR-27, X-15

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.
- `B06` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B08_recruitment_onboarding_and_offboarding\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B08_recruitment_onboarding_and_offboarding\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B08_recruitment_onboarding_and_offboarding\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B08_recruitment_onboarding_and_offboarding\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B08_recruitment_onboarding_and_offboarding\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\AutomationModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\ActivityModels.cs`

## Deliverables

- Ship the concrete outcome described by `B08` across route scope `/crm-hr/recruiting`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B08_recruitment_onboarding_and_offboarding` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03, B06`.
- Downstream dependents: `B11, B12, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- UI, component-test, and browser-proof

## Implementation Steps

1. Reopen the preserved architect docs and the live repo source references listed above.
2. Re-run the entry gate against current code before editing feature files.
3. Implement only the smallest correct change set for this subbundle and its owned stories.
4. Run the proof required for this phase and update the execution report while the evidence is fresh.
5. Run the closure gate and reopen the subbundle immediately if proof is weak or contradicted by later behavior.

## Scope Exceptions

- None pre-approved. If current repo contracts force a scope change, repair the bundle before calling the phase complete.

## Do Not Do

- Do not import CanvasLib into CRM/HR pages.
- Do not bypass current storage-placement, search, activity, or project-structure service boundaries.
- Do not replace project-local participant behavior with a forced central-directory-only model.

## Acceptance Checklist

- Candidates move through recruitment stages with history preserved.
- Interviews and feedback persist.
- Hiring conversion can create workforce identity without duplicating the person.
- Onboarding/offboarding tasks are visible and actionable.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr/recruiting`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b08\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B11, B12, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.

## Execution Notes

- Extended `/crm-hr/recruiting` from a placeholder route into a working recruiting workspace with candidate quick-create/edit, stage transitions, audit-backed stage history, structured interview scheduling, support-role assignment, lifecycle checklist management, and workforce conversion.
- Reused current live-repo services instead of adding parallel recruiting infrastructure. Candidate creation stays inside the shared party directory, workforce conversion routes through the existing HR service, visible actions publish to `IActivityStream`, and onboarding support roles are represented with current party relationships.
- Repaired stale bundle assumptions around current storage and project contracts. Recruiting timelines use `CrmHrAuditEntry` rather than a dedicated history store, lifecycle task project pickers use the current shared project queries, and SQLite-safe validation required client-side ordering for `DateTimeOffset` interview and audit sequences.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CanDoItAll.Modules.CrmHr.csproj -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter RecruitingPageTests -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter RecruitmentLifecycleIntegrationTests -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter RecruitmentFlowTests -v minimal`
- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v minimal`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b08\crm-hr-recruiting-b08-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b08\crm-hr-recruiting-b08-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b08\screenshot-review.md`

## Suggested Agent Prompt

```text
Implement B08 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B08_recruitment_onboarding_and_offboarding against the live repo files listed under Exact Source References before editing code.
```

