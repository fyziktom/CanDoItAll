# Structured Input

## Objectives

- Inventory editable form areas across shipped app modules and related shared components.
- Capture form-only screenshots for representative editable surfaces with the highest risk or widest reuse.
- Generate image proposals for each captured form area as planning references.
- Implement the smallest code changes that make form width, textarea height, grouping, and enterprise affordance clearer.
- Maintain an `.xlsx` checklist with file references, screenshots, proposals, code status, and validation status.

## Hard Constraints

- Prefer BaseLib form wrappers and Radzen-compatible shared controls.
- Improve shared components and shared CSS before adding page-specific layout fixes.
- Keep form state and validation behavior unchanged unless a layout defect forces a small markup correction.
- Generated proposals are not acceptance evidence. Browser screenshots are required for shipped proof.

## Form Surface Priorities

1. Shared BaseLib form controls and form CSS.
2. High-density process editors.
3. Workspace settings forms and secret/provider editors.
4. CRM-HR editors with many fields and textareas.
5. Agent framework dialogs and workflow editors.
6. Prompt factory editors and dialogs.
7. Project/workbench dialogs with form-like inputs.
8. Sandbox-only proof pages after product pages.
