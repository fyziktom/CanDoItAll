# Assumptions And Risks

## Assumptions

- "Each page in our app" means every routable `@page` under `src`, with product module routes prioritized over component sandbox catalog pages.
- Small route pages below roughly 300 lines are still inventoried, but only refactored when the checklist identifies real helper or component extraction value.
- Page-owned components under `Pages\Components` are in scope when they are longer than the pages that host them or are directly tied to a refactored route.
- Helper extraction should start with pure static or mostly pure formatting, filtering, node classification, editor model construction, and key-building logic.
- Component extraction should keep state ownership in the page unless the state is local to the extracted component and can be represented through typed parameters and `EventCallback`.

## Critical Path Risks

- `01-project-structure-node-helpers` is a critical foundation because later ProjectStructure component extraction depends on stable node classification and attachment preview decisions.
- `03-prompt-factory-canvas-helpers` is a critical foundation because later PromptFactory component extraction depends on canvas node/link and recommendation overlay behavior.
- `05-plugin-page-helpers-and-render-fragments` is critical for `/plugins` because connection editor state, busy keys, and OAuth actions must keep stable test ids.
- `06-crm-hr-page-helper-extraction` is a high-risk cross-route phase because helper extraction touches filters, editor factories, and sensitive-data flows across several CRM/HR routes.
- Any extraction that changes test ids, event callback timing, or selected node/session state can invalidate downstream browser proof.

## Validation Risks

- Browser proof requires seed data for project structure, prompt factory, plugins, CRM/HR, settings, and workflows; missing seed data must be recorded as an explicit proof blocker, not hidden as residual risk.
- The components MCP is currently unavailable, so layout choices need either a successful retry or local usage proof before new structural components are added.
- Long markup-only components can be split without compile failures while still regressing visual hierarchy; screenshot review is mandatory for those phases.
- Some page helpers may be private because they close over injected services or component state; those should remain page methods unless the dependency can be passed explicitly.

## Reopen Triggers

- Reopen a helper subbundle if a downstream component split discovers that extracted helpers depend on hidden page state or mutate collections unexpectedly.
- Reopen a component subbundle if Playwright or component tests show changed route behavior, lost focus/selection, broken dialog layering, clipped overlays, or different callback order.
- Reopen preparation if the workbook misses a routable page or a page-owned component over 500 lines.
- Reopen the relevant subbundle if a test id used by existing component or Playwright tests changes without an explicit acceptance criterion.
