# Canvas Feedback Bundle 2

This bundle turns `Feedback2.docx` into an implementation-ready and executable feedback pack for the shared project structure workbench.

## Profile

- `feedback`

## Mission

Close four concrete canvas feedback items without introducing page-local workarounds:

- center the help overlay in the visible canvas area
- let markdown creation accept either direct text input or an uploaded file
- make file nodes use stronger subtype-colored backgrounds
- keep PDF preview dialogs inside the canvas shell instead of behind it

## Bundle Layout

- `inputs/` raw request, source artifacts, structured restatement, extracted docx notes, and extracted screenshots
- `analysis/` verified current-state ownership and delivery risks
- `requirements/` normalized, testable requirements
- `architecture/` the target shared-workbench fix strategy
- `plan/` execution order
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` four execution-ready workstreams
- `reviews/` self-review and execution report

## Recommended Execution Order

1. `subbundles/01-center-help-window-on-visible-canvas-area`
2. `subbundles/02-add-file-upload-to-create-markdown-flow`
3. `subbundles/03-apply-file-type-node-backgrounds`
4. `subbundles/04-keep-pdf-preview-modal-above-canvas`

## Validation Summary

- Bundle preparation status: `Prepared and implementation-ready`
- Execution status: `Implemented with focused regression coverage`
