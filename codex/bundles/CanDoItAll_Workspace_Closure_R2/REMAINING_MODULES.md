# Remaining UI extraction roadmap — source-reviewed planning, not execution scope

Reference: current HEAD `15eadc18932e77a20ae5be2d7f2b207700a15d25`.
The estimates below are architectural effort bands, not execution-time promises. A "slice"
means a cohesive renderer/host boundary with production wiring, a real independent sandbox,
assets, owner/consumer tests and browser proof. Actual counts are to be finalized by a focused
source census before each assignment. Lines/file counts alone do not determine effort.

## Main remaining work

| Recommended order after closure | Family | Current state | Effort | Provisional slices and risk |
| --- | --- | --- | --- | --- |
| 1 | Projects | No Projects rendering leaf shown in the current solution. Main portfolio page and real cards/board, modal editor, hierarchy and file panes remain in the module. | 3/5 for portfolio/editor; 4/5 for whole module | About 2–3: portfolio and selection; editor/hierarchy; files/package surfaces where a separate cut is justified. Preserve lifetime admission, route-driven dialogs, CRM references, package/history and governed file effects. |
| 2 | AgentFramework remainder | Catalog, capabilities and Overview are extracted; full detail forms and several runtime/provider/dialog surfaces remain in the implementation. This is not a complete redo of Agents. | 3–4/5 | About 2–4 coherent cuts after exact census: technical editor and dependent selectors; provider/history/configuration surfaces; remaining chat/usage host renderers. Many permissions, nested dialogs and late-result paths. |
| 3 | Workflow authoring/management (within Agents + MAF) | Light Workflows.UI already exists, but WorkflowCanvasEditor still owns large actual markup in the AgentFramework module and imports Workspace/Prompt integration. | 4/5 | About 2–4: catalogue/components/run views, canvas+inspector and settings/admission dialogs. Reuse the existing light library; preserve immutable version/input, real executors and exact launch authority. |
| 4 | Workbench | Project calendar, Structure canvas and its many companion partials/asset/task/runtime surfaces remain coupled to the implementation. | 5/5 overall | About 4–6: begin with smaller calendar/read panels, then work/assignment views and native editors; Structure canvas and runtime/file/context composition last within this family. Do not change native owners or cross-module transaction protocols. |
| 5 | Processes | Thin route wrappers exist, but their components still compile with process application/runtime/persistence/drivers and feature integrations. A short Page file is not an isolated UI. | 5/5; keep last | About 4–6: catalogue/read panels, definition/configuration editing, launch/approval, run/live monitoring/recovery and final shared integration. Preserve SSE/claims/snapshots/receipts/Process versus Workflow authority. |

The next focused extraction should normally be **Projects portfolio and its real editor flow**,
after known functional blockers are closed. Do not take the entire Projects -> Workbench ->
Processes call graph as the extraction scope. A use of another module's contract does not
move ownership of that module into Projects.UI.

## Existing partial foundations and final audits

| Area | Treatment | Relative effort |
| --- | --- | --- |
| Simple Chats | Already has a light UI assembly and neutral conversation renderers; audit remaining adapters/contributors and independent scenario coverage. It is not another backend module to recreate. | 2/5 for a bounded coverage/host audit; any found large renderer needs its own estimate. |
| Web Home/dashboard and runtime capability pages | Not business modules. Home and RuntimeCapabilities are still in Web; classify reusable rendering versus legitimate composition after the major feature cuts. | 2–3/5, roughly 1–2 optional cuts after actual ownership review. |
| Security | Do not invent a standalone UI extraction without an actual remaining screen. Secrets/API administration are already in Workspace leaves. Existing policy and vault owners remain backend responsibilities. | No separate full module estimate justified by this inventory. |
| Shared AppComponents/Conversations/Configuration | Preserve established generic boundaries; repair bounded defects, do not relocate feature owners into generic UI. | Cross-cutting audit, not a new universal module. |

## Already extracted feature families

CRM/HR, Prompts, Collaboration, TestLab, Plugins, SchedulerPlanner, Memory, Resources and
Workspace have their feature rendering projects in the reviewed solution. Preserve those
extractions and use targeted regression, not new blank replacement projects. Their existence
is not a blanket assertion that every application behavior is certified. Agents/Workflow
partial completion is intentionally called out separately. [R17, R24–R28]

## Evidence and limitations

Projects' inspected folder contains its main page plus seven actual Razor components,
including file panes, modal and board; the page directly injects project/package/profile
owners. Workbench planning uses its current folder and detailed ownership README. Processes
planning uses its actual project reference graph and route inventory. Agent and Workflow
planning uses representative actual remaining markup and existing UI project descriptions.
This is not a full line-by-line residual scan of every large module. [R18–R29]

Before issuing each extraction, inventory all callers, public types, actual render descendants,
static/scoped assets, route semantics and evaluated references. Confirm the sandbox contains
production renderers rather than copies. Choose the smallest cohesive user-visible scope;
leave Processes last as requested. No exact completion percentage is defensible from the
number of projects already created.
