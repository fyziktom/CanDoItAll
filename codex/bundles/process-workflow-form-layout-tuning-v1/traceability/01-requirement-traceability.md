# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001` Processes Steps setup forms are ugly. | `requirements/01-normalized-requirements.md#r001`, `requirements/01-normalized-requirements.md#r003`, `requirements/01-normalized-requirements.md#r004` | `subbundles/02-02-process-step-form-tabs` | Processes source assertions, build, browser screenshot on `/processes` Steps. | Layout-only; no behavior change. |
| `N002` Use imagegen for separate layout proposals. | `evidence/imagegen-proposals/README.md` | `subbundles/01-01-layout-inventory-and-proposals` | Proposal files exist under `evidence/imagegen-proposals/`. | Generated proposals are not closure proof. |
| `N003` Use tabs for long process step details. | `architecture/01-target-solution.md#processes-layout` | `subbundles/02-02-process-step-form-tabs` | `ProcessStepEditorForm.razor` contains shared `Tabs`/`TabsItem` sections; browser proof. | Tab labels may differ slightly if implementation needs clearer names. |
| `N004` Repair Workflows Editor forms with same tuning. | `architecture/01-target-solution.md#workflows-layout` | `subbundles/03-03-workflow-editor-form-tabs` | `WorkflowCanvasEditor.razor` contains inspector tabs; browser proof on `/agents/workflows`. | Existing node details modal can remain sectioned if main inspector is fixed. |
| `N005` No special styling; use components and better layout. | `architecture/01-target-solution.md#boundaries` | `subbundles/02-02-process-step-form-tabs`, `subbundles/03-03-workflow-editor-form-tabs`, `subbundles/04-04-validation-and-closure` | Source assertions and anti-stub audit. | Minimal local CSS allowed only for layout mechanics. |
