# B11 - Cross-module integration with search, activity, resources, validation, test lab, and automation

## Status

- `Completed`

## Objective

- Finish enterprise integration by indexing CRM/HR artifacts, writing activity events, linking owners to resources, validation, and tests, and wiring reminder-style automation jobs.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B11_cross_module_search_activity_validation_testlab_resources_and_automation`
- Story IDs: DIR-14, DIR-15, CRM-20, PRJ-12, PRJ-13, X-02, X-03, X-08, X-15

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.
- `B04` must be completed or honestly blocked before this subbundle starts.
- `B05` must be completed or honestly blocked before this subbundle starts.
- `B06` must be completed or honestly blocked before this subbundle starts.
- `B07` must be completed or honestly blocked before this subbundle starts.
- `B08` must be completed or honestly blocked before this subbundle starts.
- `B09` must be completed or honestly blocked before this subbundle starts.
- `B10` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B11_cross_module_search_activity_validation_testlab_resources_and_automation\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B11_cross_module_search_activity_validation_testlab_resources_and_automation\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B11_cross_module_search_activity_validation_testlab_resources_and_automation\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B11_cross_module_search_activity_validation_testlab_resources_and_automation\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B11_cross_module_search_activity_validation_testlab_resources_and_automation\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Search\SearchIndexing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\ActivityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\ResourceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\ValidationModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\TestLabModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\AutomationModels.cs`

## Deliverables

- Ship the concrete outcome described by `B11` across route scope `/activity, /resources, /validation, /test-lab, /automation, /crm-hr`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B11_cross_module_search_activity_validation_testlab_resources_and_automation` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03, B04, B05, B06, B07, B08, B09, B10`.
- Downstream dependents: `B12, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Critical cross-module integration foundation

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

- CRM/HR entities appear in global search where safe.
- Major CRM/HR actions appear in Activity.
- Resources/Validation/Test Lab can reference responsible parties.
- Automation workspace can show CRM/HR reminder jobs or equivalent status.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/activity, /resources, /validation, /test-lab, /automation, /crm-hr`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b11\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B12, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.
- Because this is a critical foundation, at least one dependent-flow smoke must pass before downstream work may continue.

## Execution Notes

- Reused the live search, activity, project-party, and automation boundaries instead of adding a parallel CRM-HR integration layer. Search documents and activity entries stay owned by the current module services, while automation now consumes a small shared `IAutomationSignalProvider` contract with a CRM-HR implementation and a safe null fallback.
- Extended `/crm-hr/directory` with a party activity timeline and project-assignment panel so the new cross-module visibility lands on the existing shared identity surface rather than a second summary page.
- Added project-party backed owner and maintainer editing on `/resources` plus responsible-party round-trips on `/validation` and `/test-lab`, using the current B10 bridge rather than direct CRM-HR-to-project dependencies.
- Closure repaired stale bundle assumptions around browser proof by using the real Playwright fixture database and a dedicated B11 flow that seeds current-repo-safe CRM, recruiting, project-assignment, and accountability data before exercising the six required routes.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -nologo -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -nologo -v minimal --filter "FullyQualifiedName~CrossModuleResponsiblePartyPageTests|FullyQualifiedName~ResourcesPageTests|FullyQualifiedName~CrmHrDirectoryPageTests"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -nologo -v minimal --filter "FullyQualifiedName~CrmHrCrossModuleIntegrationTests|FullyQualifiedName~CrmInteractionIntegrationTests|FullyQualifiedName~RecruitmentLifecycleIntegrationTests|FullyQualifiedName~AiAgentProfileIntegrationTests|FullyQualifiedName~ValidationServiceIntegrationTests"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -nologo -v minimal --filter CrmHrCrossModuleFlowTests`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b11\crm-hr-activity-b11-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b11\crm-hr-directory-b11-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b11\crm-hr-directory-b11-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b11\crm-hr-resources-b11-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b11\crm-hr-validation-b11-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b11\crm-hr-testlab-b11-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b11\crm-hr-automation-b11-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b11\screenshot-review.md`

## Suggested Agent Prompt

```text
Implement B11 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B11_cross_module_search_activity_validation_testlab_resources_and_automation against the live repo files listed under Exact Source References before editing code.
```

