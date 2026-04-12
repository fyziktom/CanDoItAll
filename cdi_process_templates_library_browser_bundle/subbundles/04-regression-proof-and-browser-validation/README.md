# Regression Proof And Browser Validation

## Status

- `Completed`

## Objective

- Add and run the nearby regression coverage that proves the new browser works in both component tests and the real browser.

## Covered Inputs

- Fully validate and test the result.
- Prove the notification stays above the modal.
- Prove the modal stays open after imports.
- Prove mermaid, markdown, json, and tree previews all work.

## Prerequisites

- `subbundles/01-library-foundation-and-preview-models`
- `subbundles/02-fullscreen-template-dialog-and-list-shell`
- `subbundles/03-preview-renderers-and-selective-import-flows`

## Exact Source References

- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProcessManagementBundle.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs

## Deliverables

- Component regression tests covering modal shell and selective import behavior.
- Updated Playwright smoke proof covering the template browser workflow.
- Browser screenshots and artifact paths for the new UI states.

## Dependency Impact

- The final closure phase depends on this proof because the request is explicitly UI-heavy and depends on script-backed integrations.
- Weak proof here would make the bundle closure untrustworthy.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Add component tests for modal state, category filtering, and selective import mutations.
2. Extend the existing Playwright process-management smoke test or add a nearby focused test for the templates browser.
3. Capture browser artifacts for the fullscreen modal, preview pane, import success, and toast overlay.
4. Review the artifacts before closing the phase.

## Scope Exceptions

- Final documentation updates close in the final subbundle.

## Do Not Do

- Do not rely on build-only validation for the mermaid and notification requirements.
- Do not skip screenshot review after the Playwright pass.

## Acceptance Checklist

- Component tests cover the new modal and selective import paths.
- Playwright proof covers the templates browser workflow on `/processes`.
- Screenshots show the modal, preview, import success, and toast stacking.
- No new failing tests are introduced in the targeted suites.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Process_management_canvas_bundle_flows_are_validated_in_browser" -v:minimal`
- Screenshot artifact paths under `C:\repositories\CanDoItAll\output\playwright\process-management-bundle`

## Browser Validation Logging

- Route under test: `/processes`
- Required viewports: desktop `1900x1200`
- Required Playwright actions: open templates modal, exercise category search, inspect preview tabs, import process, import role, import artifact, verify modal persistence, verify toast overlay.
- Required screenshots: modal overview, preview pane, role import, artifact import, toast over modal.
- Required screenshot review questions: can the user scan the categories quickly, can they verify diagram detail, and is the feedback visible without closing the modal.

## Progression Gate

- Final closure may continue only after all targeted tests pass and browser artifacts confirm the requested UX.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Add targeted regression coverage and capture browser proof for the new process templates browser.
```
