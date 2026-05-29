# SB09 Semantic Invariants

## Invariant SB09-UI-CATALOG-HONESTY

- Source raw note: RN04 and R10 require templates and UI catalog entries to reflect actual runnable capabilities.
- Expected behavior: runnable executors are marked available, approval-required executors are labeled, deterministic preview is visible where true, and planned executors remain disabled/planned.
- Disallowed shallow implementation: showing helper templates or toolbox items without surfacing runtime availability and approval constraints.
- Positive proof: `Workflows_templates_tab_lists_executor_catalog_examples` and `Workflow_canvas_toolbox_exposes_executor_catalog_metadata` in `bundle://proof/SB10/transcripts/dotnet-test-component-workflows-page.txt`
- Browser proof: `bundle://proof/SB09/browser/`
- Source proof: `bundle://proof/SB10/transcripts/source-assertions-template-ui.txt`
