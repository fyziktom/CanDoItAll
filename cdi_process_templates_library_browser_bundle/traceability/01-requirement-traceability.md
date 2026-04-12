# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Replace `Seed development baseline` with `Templates`. | `requirements/01-normalized-requirements.md` | `subbundles/02-fullscreen-template-dialog-and-list-shell` | `ProcessWorkspace` component test plus Playwright modal-open proof | Entry-point rename must update header and empty state. |
| Fullscreen modal with searchable tabbed left list and right preview. | `requirements/01-normalized-requirements.md` | `subbundles/02-fullscreen-template-dialog-and-list-shell` | Component test for modal state and browser screenshot proof | Must stay aligned with BaseLib dialog and list-detail shell. |
| Render markdown, json, mermaid, and structure tree. | `requirements/01-normalized-requirements.md` | `subbundles/03-preview-renderers-and-selective-import-flows` | Component test for preview selection plus Playwright proof | Mermaid proof must be browser-backed. |
| Use `MermaidJS.Blazor`, `Markdig`, and `JsonViewer.Blazor`. | `inputs/01-source-artifacts.md` | `subbundles/01-library-foundation-and-preview-models` | Build plus runtime preview proof | DI must be updated for MermaidJS. |
| Support pan and zoom on mermaid diagrams. | `requirements/01-normalized-requirements.md` | `subbundles/03-preview-renderers-and-selective-import-flows` | Playwright interaction proof and screenshot review | Requires repo-owned JS integration around Mermaid output. |
| Add full process templates to the process library. | `requirements/01-normalized-requirements.md` | `subbundles/03-preview-renderers-and-selective-import-flows` | Component test plus browser import proof | Must reuse `ProcessTemplateProjectionService` import seam. |
| Add just a role from a process preview. | `requirements/01-normalized-requirements.md` | `subbundles/03-preview-renderers-and-selective-import-flows` | Component test plus browser proof | Must not require importing the whole process first. |
| Add artifact templates without closing the modal. | `requirements/01-normalized-requirements.md` | `subbundles/03-preview-renderers-and-selective-import-flows` | Component test for target-step import and Playwright proof | Bound to current domain constraint that artifacts live on steps. |
| Show notifications above the modal and keep the modal open. | `requirements/01-normalized-requirements.md` | `subbundles/02-fullscreen-template-dialog-and-list-shell` | Browser screenshot proof | Likely requires notification z-index adjustment. |
