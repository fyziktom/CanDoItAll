# B10 - Project and Workbench party assignment integration

## Status

- `Completed`

## Objective

- Connect projects and project-structure nodes to the new directory so customer, partner, delivery unit, participant, meeting, work item, and AI-agent assignment flows all use the unified Party model.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B10_project_and_workbench_party_assignment_integration`
- Story IDs: PRJ-01, PRJ-02, PRJ-03, PRJ-04, PRJ-05, PRJ-06, PRJ-07, PRJ-08, PRJ-09, PRJ-10, PRJ-15, PRJ-16, CRM-22, HR-14, HR-15, HR-31, AI-05

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.
- `B06` must be completed or honestly blocked before this subbundle starts.
- `B09` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B10_project_and_workbench_party_assignment_integration\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B10_project_and_workbench_party_assignment_integration\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B10_project_and_workbench_party_assignment_integration\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B10_project_and_workbench_party_assignment_integration\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B10_project_and_workbench_party_assignment_integration\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCreateRequestComposer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeEditor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\ProjectObjectContracts.cs`

## Deliverables

- Ship the concrete outcome described by `B10` across route scope `/projects, /projects/{ProjectId}/structure, /projects/{ProjectId}/calendar, /crm-hr/assignments`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B10_project_and_workbench_party_assignment_integration` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03, B06, B09`.
- Downstream dependents: `B05, B07, B11, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Critical integration foundation

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

- Projects show primary related parties on list or detail surfaces.
- Workbench participant creation can pick existing parties or create new ones.
- Meeting and work-item editors can assign central parties.
- Project-local-only participants remain supported.
- No existing structure flow is broken by central-party integration.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/projects, /projects/{ProjectId}/structure, /projects/{ProjectId}/calendar, /crm-hr/assignments`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b10\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B05, B07, B11, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.
- Because this is a critical foundation, at least one dependent-flow smoke must pass before downstream work may continue.

## Execution Notes

- Repaired the stale architect assumption that Projects could depend directly on CRM-HR. The shipped implementation introduces a small `CanDoItAll.Modules.Projects` bridge contract that CRM-HR implements, which keeps module boundaries intact while still letting project surfaces render related-party context.
- Extended `/projects` and the project modal summary to surface primary customer, delivery unit, owner, and relationship pills, plus relationship-aware filtering for portfolio review.
- Replaced the stub `/crm-hr/assignments` surface with a working project-assignment workspace that supports central party selection, allocation fields, and project-structure navigation.
- Added participant, meeting, and work-item party editors to `/projects/{ProjectId}/structure`, including quick-create from participant flow, project-local-only participant mode, meeting default copy from project assignments, and central work-item assignee sync.
- Repaired a live-repo selection defect discovered during closure: support-panel tree selection now reloads the party editor, and the editor no longer exposes actionable controls before its party data finishes loading.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter ProjectPartyAssignmentIntegrationTests -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProjectStructurePartyPickerTests -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProjectsCrmHrIntegrationTests -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter ProjectPartyAssignmentFlowTests -v minimal`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b10\crm-hr-assignments-b10-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b10\crm-hr-projects-b10-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b10\crm-hr-structure-b10-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b10\crm-hr-structure-b10-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b10\crm-hr-calendar-b10-desktop.png`

## Suggested Agent Prompt

```text
Implement B10 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B10_project_and_workbench_party_assignment_integration against the live repo files listed under Exact Source References before editing code.
```

