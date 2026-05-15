# Normalized Requirements

| Requirement | Description | Success criteria | Owning subbundle |
| --- | --- | --- | --- |
| `R001` | Inventory every routable app page and long page-owned component. | Workbook checklist includes file, route or host page, line metrics, refactor type, priority, planned subbundle, event/state risk, tests, and status. | preparation |
| `R002` | Extract ProjectStructure node helpers into `ProjectStructureNodeHelpers`. | Pure node helpers are removed from `ProjectStructurePage.razor`, tests still pass, and downstream page/component behavior is unchanged. | `01` |
| `R003` | Extract PromptFactory canvas and formatting helpers before markup decomposition. | Canvas node/link builders and pure helper methods are isolated without changing session graph behavior or Playwright-visible flow. | `03` |
| `R004` | Reduce Plugins page helper/render-fragment density. | Busy-key, tone, test-id, connection editor, and render-fragment helper logic is isolated with stable component tests. | `05` |
| `R005` | Extract CRM/HR filters, formatting helpers, and editor factories from long CRM/HR pages. | Directory, CRM, workforce, recruiting, agents, and assignments flows preserve filtering, sensitive-data handling, and navigation. | `06` |
| `R006` | Extract workspace settings helpers from large settings panels. | Database sources and storage settings helper logic moves behind typed helpers while settings tests remain stable. | `07` |
| `R007` | Split ProjectStructure page shell and oversized ProjectStructure page-owned components. | Page markup is decomposed into typed components without changing canvas, dialog, window, selection, or attachment behavior. | `02` |
| `R008` | Split PromptFactory page shell into focused components. | PromptFactory page delegates large shell regions while keeping build, save, selection, and canvas actions stable. | `04` |
| `R009` | Split process/workflow editor markup where it is page-sized. | Workflow canvas and process editor surfaces have focused components and route/browser proof. | `08` |
| `R010` | Clean up remaining route pages when the inventory marks them necessary. | Projects, scheduler, prompt gallery, test lab, validation, and similar medium pages are refactored only where the checklist shows real payoff. | `09` |
| `R011` | Preserve functionality with tests and browser proof. | Targeted component/unit tests, build, route smoke checks, screenshots, and raw-note closure rows are complete. | `10` |

## Scope Rules

- Do not refactor tiny pages just to satisfy a numeric target; record them as reviewed in the workbook.
- Do not move application service calls into UI helper classes.
- Do not introduce new interfaces for one trivial implementation.
- Do not introduce fallback behavior that masks missing state, unavailable services, or failed persistence.
- Do not replace shared component wrappers with ad hoc `div` structures when BaseLib or CanvasLib components already fit.
