# Implementation Prompt

Implement only the active subbundle from `codex/bundles/workflow-template-dialog-catalog`.

Rules:

- Reopen the active subbundle README, `plan/01-phase-plan.md`, and `traceability/01-requirement-traceability.md` before editing production code.
- Use existing CanDoItAll/BaseLib components first. If custom CSS is needed, keep it scoped to `WorkflowsPage.razor.css` and explain why shared components were insufficient.
- Preserve separation: page orchestration in `WorkflowsPage.razor.cs`, markup in `.razor`, styling in `.razor.css`, workflow template content in `Templates/Workflows`.
- Do not load `WorkflowTemplatePackLoader.Load()` except from the template catalogue dialog open path or explicit preview/adoption paths that already opened the catalogue.
- Do not create user drafts as managed seed examples.
- Use deterministic draft naming: base name first, then `01 <base>`, `02 <base>`, etc.
- Keep UI validation large-screen only.
- Update bundle proof and execution report before closing each subbundle.
