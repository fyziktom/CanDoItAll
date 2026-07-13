# Scope Inventory

## In Scope

| Area | Current surfaces | Why in scope |
| --- | --- | --- |
| Workflow contracts/models | `AgentFramework.Models/Workflows`, `Core/Workflows` | Foundation for all project moves. |
| Workflow builders/factories | Existing ad hoc construction in template loader, tests, UI, Workbench | Required by user and needed to reduce duplicated graph construction. |
| Workflow core services | validator, catalog, routing, payload, preview, failure formatter | Currently mixed in Core. |
| Workflow runtime/stores | runtime manager, backend catalog, run/checkpoint/artifact/external request stores | Needed for testable runtime and persistence boundaries. |
| Executor abstractions/helpers | executor interfaces, catalog, invoker, observability, redaction, policies | Must have own projects. |
| Default executor implementations | MAF runtime workflow executors | Currently in MAF and mixed by category. |
| Plugin executor integration | `Modules.Plugins`, `Plugins.Abstractions`, bundled plugins, runtime packages | Plugins are source of many executors. |
| Workflow templates | `Templates/Workflows`, template loader | File-driven workflow definition source. |
| MAF adapter | MAF compiler/backend/LLM invoker/event normalizer | MAF should be adapter, not owner. |
| Host composition | `AgentFrameworkServiceCollectionExtensions` | Current large registration block hides boundaries. |
| API/UI/Workbench | Workflows API, workflow page/canvas, project-structure workflow nodes | Adoption and regression surfaces. |
| Tests and proof | unit, integration, component, Playwright tests | Required for base-up hardening and regression proof. |

## Out Of Scope For This Bundle

| Area | Reason |
| --- | --- |
| Reworking non-workflow process driver architecture | Processes already have dedicated projects and are only touched for compatibility proof. |
| Changing the public meaning of workflow definitions or executor ids | Compatibility invariant. |
| Rewriting plugin marketplace/package installation unrelated to executor discovery | Only workflow executor registration/projection consequences are in scope. |
| UI redesign | UI adoption is limited to service boundary changes and regression proof. |
| Performance rewrites without profiling or checkpoint evidence | Hardening checkpoints decide targeted changes. |
