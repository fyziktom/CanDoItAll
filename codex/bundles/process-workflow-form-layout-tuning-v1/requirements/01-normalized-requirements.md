# Normalized Requirements

| Requirement | Source notes | Observable acceptance |
| --- | --- | --- |
| `R001` Preserve and analyze the current process/workflow form surfaces before implementation. | `N001`, `N004` | Bundle analysis lists exact process and workflow source files plus current layout problem. |
| `R002` Create separate imagegen layout proposals for the affected process and workflow setup forms. | `N002` | Proposal images exist under `bundle://evidence/imagegen-proposals/` and are mapped to surfaces. |
| `R003` Split the long Process Step Details form into tabs by intent. | `N001`, `N003`, `N005` | `ProcessStepEditorForm.razor` uses `Tabs`/`TabsItem` and groups fields into Basic info, Execution, Contracts, Routing, Roles, and Artifacts or equivalent sections. |
| `R004` Keep process child setup forms compact and readable inside their parent tabs. | `N001`, `N002`, `N005` | Branch outcome, role assignment, and artifact expectation editors remain separate components and use compact `Grid`/`Stack` grouping without special styling. |
| `R005` Apply the same tabbed organization to the Workflows Editor inspector. | `N004`, `N005` | `WorkflowCanvasEditor.razor` separates Definition, Node setup, Routes, and Preview/Validation forms with shared tabs; executor settings stay in Node setup to preserve selected-node handler scope. |
| `R006` Preserve existing behavior and component vocabulary. | `N005` | Builds pass; source assertions show no domain/service/persistence changes and no placeholder or decorative style implementation. |
| `R007` Validate rendered layout in a browser. | `N001`, `N003`, `N004`, `N005` | Execution report includes desktop and narrow browser evidence for `/processes` Steps and `/agents/workflows` Editor. |

## Scope Exceptions

- The generated image proposals are planning artifacts only; the final UI is implemented with existing Blazor components and may not match every pixel of the generated images.
- No new reusable component is planned unless implementation proves existing shared components cannot express the layout.
