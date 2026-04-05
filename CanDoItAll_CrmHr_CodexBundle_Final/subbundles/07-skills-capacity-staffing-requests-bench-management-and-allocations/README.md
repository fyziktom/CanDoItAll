# B07 - Skills, capacity, staffing requests, bench management, and allocations

## Status

- `Completed`

## Objective

- Implement skill catalog handling, proficiency, certifications, availability blocks, staffing requests, project allocations, bench views, and demand-versus-capacity reporting.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B07_skills_capacity_staffing_and_allocations`
- Story IDs: HR-07, HR-08, HR-09, HR-10, HR-11, HR-12, HR-13, HR-14, HR-15, HR-18, HR-31, HR-32, HR-33, HR-35, PRJ-14

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.
- `B06` must be completed or honestly blocked before this subbundle starts.
- `B10` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B07_skills_capacity_staffing_and_allocations\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B07_skills_capacity_staffing_and_allocations\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B07_skills_capacity_staffing_and_allocations\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B07_skills_capacity_staffing_and_allocations\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B07_skills_capacity_staffing_and_allocations\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`

## Deliverables

- Ship the concrete outcome described by `B07` across route scope `/crm-hr/assignments, /crm-hr/workforce`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B07_skills_capacity_staffing_and_allocations` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03, B06, B10`.
- Downstream dependents: `B11, B13`.
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

- Skills and proficiency are searchable from workforce/assignment pages.
- Staffing requests can be created with role, skills, dates, and allocation.
- Allocations affect capacity views and conflict callouts appear.
- Project-linked allocations are visible from both HR and project context.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr/assignments, /crm-hr/workforce`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b07\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B11, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.

## Execution Notes

- Extended `/crm-hr/workforce` with skill and availability filtering, bench and overload summary tiles, a dedicated skill-matrix editor, and a capacity timeline that surfaces project-linked allocations and conflict callouts from the same workspace.
- Extended `/crm-hr/assignments` with staffing dashboard cards, staffing-request editing, candidate search by skill and availability, and allocation editing that reuses the B10 project-party assignment bridge instead of introducing a separate allocation store.
- Repaired a stale bundle assumption around search and storage behavior: workforce search now includes skill proficiency text, and SQLite-backed validation required client-side ordering for staffing, capacity, and assignment query shapes that the preserved architect bundle did not account for.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "CrmHrWorkforcePageTests|AssignmentsPageTests" -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "StaffingAllocationIntegrationTests|ProjectPartyAssignmentIntegrationTests" -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter StaffingFlowTests -v minimal`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b07\crm-hr-assignments-b07-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b07\crm-hr-assignments-b07-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b07\crm-hr-workforce-b07-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b07\crm-hr-workforce-b07-tablet.png`

## Suggested Agent Prompt

```text
Implement B07 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B07_skills_capacity_staffing_and_allocations against the live repo files listed under Exact Source References before editing code.
```

