# B13 - Validation hardening, rollout, migration rehearsal, and regression suite

## Status

- `Completed`

## Objective

- Create the final quality gate: broad automated tests, Playwright coverage, screenshot semantics, seed data rehearsal, migration verification, and rollout/rollback notes.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B13_validation_rollout_and_regression_suite`
- Story IDs: X-06, X-07, X-13, X-16

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
- `B11` must be completed or honestly blocked before this subbundle starts.
- `B12` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B13_validation_rollout_and_regression_suite\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B13_validation_rollout_and_regression_suite\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B13_validation_rollout_and_regression_suite\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B13_validation_rollout_and_regression_suite\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B13_validation_rollout_and_regression_suite\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PlaywrightAppFixture.cs`

## Deliverables

- Ship the concrete outcome described by `B13` across route scope `/crm-hr, /projects, /activity, /resources, /validation, /test-lab`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B13_validation_rollout_and_regression_suite` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12`.
- Downstream dependents: `none`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- End-to-end regression and closure

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

- Component, integration, and Playwright tests exist for the final CRM/HR surface.
- Evidence folders contain screenshots plus semantic review notes.
- Fresh-db startup and seeded defaults are proven.
- The final QA gate can be executed repeatably.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr, /projects, /activity, /resources, /validation, /test-lab`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b13\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- No downstream subbundles depend on this phase; closure can proceed directly to final bundle validation once acceptance and proof are complete.

## Execution Notes

- B13 intentionally reused the live repo’s phase-specific component and integration suites instead of creating a second monolithic regression layer. The final gate is a curated command set over the shipped CRM-HR tests plus one dedicated Playwright regression pass that captures the final route set required by the bundle.
- Added `tests/CanDoItAll.Tests.Playwright/CrmHrRegressionTests.cs` to seed a realistic end-state CRM-HR scenario, exercise `/crm-hr`, `/projects`, `/activity`, `/resources`, `/validation`, and `/test-lab`, and write a final B13 browser artifact set with a semantic screenshot review.
- Closure exposed one real final-gate regression: the original shell smoke used a strict role locator for `Open directory`, which became ambiguous after B12 added a second home-page action. The fix was the smallest correct one: explicit home-page test ids on the two buttons plus a targeted update to `CrmHrShellSmokeTests`.
- Final rollout and rollback rehearsal notes are captured in `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\reviews\02-rollout-and-rollback-notes.md`, and the build plus integration gate proves the current migration assemblies and fresh-db startup path still hold under the final module shape.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -nologo -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -nologo -v minimal --filter "FullyQualifiedName~CrmHrNavigationTests|FullyQualifiedName~CrmHrDirectoryPageTests|FullyQualifiedName~CrmPageTests|FullyQualifiedName~CrmHrWorkforcePageTests|FullyQualifiedName~RecruitingPageTests|FullyQualifiedName~AiAgentsPageTests|FullyQualifiedName~AssignmentsPageTests|FullyQualifiedName~ProjectsCrmHrIntegrationTests|FullyQualifiedName~CrossModuleResponsiblePartyPageTests|FullyQualifiedName~CrmHrPrivacyBoundaryTests"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -nologo -v minimal --filter "FullyQualifiedName~CrmHrSchemaIntegrationTests|FullyQualifiedName~DatabaseMigrationIntegrationTests|FullyQualifiedName~CrmInteractionIntegrationTests|FullyQualifiedName~OpportunityConversionIntegrationTests|FullyQualifiedName~WorkforceProfileIntegrationTests|FullyQualifiedName~StaffingAllocationIntegrationTests|FullyQualifiedName~RecruitmentLifecycleIntegrationTests|FullyQualifiedName~AiAgentProfileIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests|FullyQualifiedName~CrmHrCrossModuleIntegrationTests|FullyQualifiedName~CrmHrAuditTrailIntegrationTests"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -nologo -v minimal --filter "FullyQualifiedName~CrmHrShellSmokeTests|FullyQualifiedName~CrmHrRegressionTests"`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b13\crm-hr-home-b13-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b13\crm-hr-home-b13-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b13\crm-hr-projects-b13-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b13\crm-hr-activity-b13-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b13\crm-hr-resources-b13-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b13\crm-hr-validation-b13-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b13\crm-hr-testlab-b13-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b13\screenshot-review.md`
- Rollout notes: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\reviews\02-rollout-and-rollback-notes.md`

## Suggested Agent Prompt

```text
Implement B13 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B13_validation_rollout_and_regression_suite against the live repo files listed under Exact Source References before editing code.
```

