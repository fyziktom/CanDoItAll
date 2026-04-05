# B02 - Directory shell, navigation, routes, and core BaseLib pages

## Status

- `Completed`

## Objective

- Add the CRM / HR shell entry, root pages, route structure, summary dashboard, directory workspace, and BaseLib-first page composition without using canvas components.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B02_directory_shell_navigation_and_core_pages`
- Story IDs: DIR-03, DIR-14, DIR-15, CRM-18, CRM-19, HR-35, AI-08, X-01, X-04, X-13

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B02_directory_shell_navigation_and_core_pages\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B02_directory_shell_navigation_and_core_pages\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B02_directory_shell_navigation_and_core_pages\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B02_directory_shell_navigation_and_core_pages\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B02_directory_shell_navigation_and_core_pages\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\docs\ui-shared-components\README.md`

## Deliverables

- Ship the concrete outcome described by `B02` across route scope `/crm-hr, /crm-hr/directory, /crm-hr/crm, /crm-hr/workforce, /crm-hr/recruiting, /crm-hr/agents, /crm-hr/assignments`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B02_directory_shell_navigation_and_core_pages` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01`.
- Downstream dependents: `B03, B04, B05, B06, B07, B08, B09, B10, B11, B12, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Critical UI foundation

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

- Navigating to `/crm-hr` and the child routes works without shell errors.
- The Directory page can create and edit a basic party record.
- All CRM/HR pages use BaseLib-first layouts and do not import canvas libraries.
- Playwright smoke flow proves navigation, save, and reload persistence.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr, /crm-hr/directory, /crm-hr/crm, /crm-hr/workforce, /crm-hr/recruiting, /crm-hr/agents, /crm-hr/assignments`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b02\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B03, B04, B05, B06, B07, B08, B09, B10, B11, B12, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.
- Because this is a critical foundation, at least one dependent-flow smoke must pass before downstream work may continue.

## Suggested Agent Prompt

```text
Implement B02 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B02_directory_shell_navigation_and_core_pages against the live repo files listed under Exact Source References before editing code.
```

