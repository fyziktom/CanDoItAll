# B05 - Opportunities, pipeline, stage history, and project conversion

## Status

- `Completed`

## Objective

- Build the opportunity board, structured stage progression, stage history, partner-sourced deals, lost reasons, and conversion of won opportunities into CanDoItAll project context.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B05_opportunities_pipeline_and_project_conversion`
- Story IDs: CRM-06, CRM-07, CRM-08, CRM-09, CRM-10, CRM-13, CRM-15, CRM-16, CRM-18, CRM-24, PRJ-11

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.
- `B04` must be completed or honestly blocked before this subbundle starts.
- `B10` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B05_opportunities_pipeline_and_project_conversion\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B05_opportunities_pipeline_and_project_conversion\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B05_opportunities_pipeline_and_project_conversion\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B05_opportunities_pipeline_and_project_conversion\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B05_opportunities_pipeline_and_project_conversion\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`

## Deliverables

- Ship the concrete outcome described by `B05` across route scope `/crm-hr/crm`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B05_opportunities_pipeline_and_project_conversion` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03, B04, B10`.
- Downstream dependents: `B11, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Critical business-flow integration

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

- Opportunities can move across stages and stage history is recorded.
- Won opportunity conversion creates or links a project and keeps party context.
- Lost opportunities keep loss reason and are still historically visible.
- Pipeline UI is readable and validated with screenshots.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr/crm`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b05\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B11, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.

## Execution Notes

- Repaired stale bundle assumptions against the live repo by using the shipped B10 project-party bridge and the current CRM/HR home contract, which surfaces open pipeline preview instead of all historical opportunities.
- Replaced the old placeholder opportunity section on `/crm-hr/crm` with a real pipeline surface: stage board, search and relationship filters, stage-aware editor, partner-linked parties, lost-reason handling, and project conversion dialog.
- Extended CRM opportunity persistence so owner, delivery unit, amount, probability, expected close, partner contribution, competitor context, lost reason, search indexing, stage history, and audit/activity updates all round-trip through one typed service contract.
- Won opportunity conversion now creates or links a project through `ProjectsService` and preserves relevant party context through the B10 project assignment bridge instead of duplicating relationship data manually.
- Added open-pipeline preview on `/crm-hr` and captured browser proof showing the CRM route, linked project navigation, reload persistence, and home preview behavior under the current UI.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "OpportunityBoardTests|CrmPageTests" -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "OpportunityConversionIntegrationTests|CrmInteractionIntegrationTests" -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter OpportunityPipelineTests -v minimal`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b05\crm-hr-crm-b05-initial.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b05\crm-hr-crm-b05-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b05\crm-hr-crm-b05-reload.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b05\crm-hr-crm-b05-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b05\crm-hr-projects-b05-linked-project.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b05\crm-hr-home-b05-desktop.png`

## Suggested Agent Prompt

```text
Implement B05 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B05_opportunities_pipeline_and_project_conversion against the live repo files listed under Exact Source References before editing code.
```

