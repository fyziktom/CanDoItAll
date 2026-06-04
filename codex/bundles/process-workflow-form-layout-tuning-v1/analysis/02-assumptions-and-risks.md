# Assumptions And Risks

## Assumptions

- The user wants actual product UI changes, not only mockups.
- Existing shared components are preferred over a new CSS layout system.
- Inner tabs can use component-local integer state because the state is presentation-only and does not cross module boundaries.
- Existing test IDs should remain wherever browser or component tests may depend on them.

## Critical Path Risks

- `WorkflowCanvasEditor.razor` is a large component with many inline event handlers; moving markup into tabs must not detach variables such as `selected`, `descriptor`, `policy`, and executor settings locals from their scope.
- `ProcessStepEditorForm.razor` is reused both in the Steps tab and in the canvas editor floating window. A tabbed layout must work in both contexts, including narrower floating-window widths.
- Workflow executor setup has multiple conditional forms. The tab split must keep every executor settings branch reachable.

## Validation Risks

- Local browser proof may require an active database profile and seeded process/workflow data.
- Imagegen proposals can guide layout but cannot prove the real component output.
- Build proof may surface unrelated existing warnings; closure must separate those from regressions caused by this change.

## Reopen Triggers

- Browser proof shows a tab panel with missing required controls, overlapped text, unusable action buttons, or lateral overflow.
- A build fails due to Razor scope or event-handler errors from moved markup.
- Source assertions show a requested form still remains as one long mixed stack.
- The implementation introduces new special styling beyond minimal layout support.
