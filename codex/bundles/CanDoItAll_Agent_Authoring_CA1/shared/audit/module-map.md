# Current module and surface map

Observation at the review SHA; **not a complete source-level audit or test certification of each module**. Project references are direct metadata observations unless stated otherwise. Re-evaluate imported references and discover actual routes/rendered closure on the child branch. Do not use this table as a hard-coded implementation sequence.

## Established seams to reuse

| Surface / family | Current evidence | Implication for the next slice |
|---|---|---|
| Agents rendering/catalog | `src/UI/CanDoItAll.AgentFramework.UI`, catalog sandbox scripts; references include light Models/Usage/ProviderHistory abstractions and Conversations.Components [S08, S38]. | Preserve existing extraction; inspect the actual changed agent surface instead of restarting an old Agents pilot. A library's existence does not certify the whole agent module. |
| Prompt Gallery | `CanDoItAll.Prompts.UI`, `Modules.Prompts.Contracts`, module host/sessions, Prompts sandbox [S04, S08, S09, S22]. | Reuse presentation/intent and owner sessions. Keep accepted baseline/conflict semantics; consider the named CTS cleanup when touching that code. |
| CRM / HR | `CanDoItAll.CrmHr.UI`, CrmHr.Contracts, narrow Projects.Contracts; maintained map of Home, Directory, CRM, Workforce, Recruiting, Agents, Assignments; CRM sandbox [S05, S10–S16, S23]. | Treat the completed extraction map as precedent, not a new module to redo. Still verify changed hosts, reads, shared children and actual current tests. |
| Paged record family | `CanDoItAll.AppComponents.RecordBrowsing`; current CRM and its completion record use the real family [S05, S10, S39]. | Reuse it without importing all AppComponents or duplicating a picker implementation. |
| Workflow presentation | `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.UI` has ASP.NET components, BaseLib and Charts references [S17]. | UI already exists outside `src/UI`; inspect its actual coverage before adding another workflow UI project. |
| Simple Chats presentation | `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.UI` references BaseLib and Conversations.Components [S18]. | Reuse neutral conversation rendering while keeping Simple Chats behavior distinct from agents. |
| Conversations / application shared UI | UI tree contains Conversations.Components, Conversations.Shell, AppComponents and Components.Git [S39]. | Inspect each required closure. Neither “Shell” nor “Components” in a name proves the assembly is suitable for a backend-free sandbox. Their full implementation closure was not audited here. |

## Remaining mixed module candidates

These are candidate **owner/surface boundaries**, not instructions to extract every line or create one sandbox per row.

| Module | Observed metadata / coupling candidate | Preparation focus |
|---|---|---|
| Collaboration | Razor + BaseLib + Infrastructure + SharedKernel [S36]. | Discover actual active components and owner queries. Relatively few direct dependencies, but not enough evidence to claim a small implementation effort. |
| TestLab | Razor + BaseLib + Infrastructure + Projects [S31]. | Separate test/result presentation from project/persistence actions; check existing Projects.Contracts coverage before extracting more. |
| Memory | Razor + Memory.Abstractions/Application/Http/Mcp + SharedKernel [S29]. | Render provider-neutral values; keep application and adapter configuration at the production owner. Do not enable Memory providers in a preview host. |
| Plugins | Razor + EF + Infrastructure/Security + agent/workflow/executor implementations [S30]. | Isolate catalog/settings/status; retain permissions, install/execution effects and runtime registrations at their owners. |
| SchedulerPlanner | Razor + Quartz hosting + Workflows.Runtime + agent tooling + Infrastructure [S28]. | Render schedule/status/editor without starting Quartz or execution. Preserve dispatch semantics and any existing scope/control plane. |
| Projects | Razor + existing Projects.Contracts + Infrastructure + AgentFramework.Core + FileTools/AppComponents [S26]. | Existing contracts are a narrow slice, not whole-module completion. Preserve project lifecycle/admission and real file/assignment consumers. |
| Workspace | Razor + Infrastructure/Projects/Security + identity/http and provider-history contracts [S33]. | Identify actual settings/workspace surfaces, separate secrets/authorization and owner commands, reuse narrow existing values. |
| Resources | Razor + Infrastructure + FileTools.Integration + agent/Memory application + Projects/Security/Workspace [S27]. | Separate resource catalog/editor from bytes, authorization, project attachment and runtime effects. Avoid wrapping the same heavy graph in a new UI facade. |
| Processes | Razor + EF/Npgsql + existing Processes Application/Builder/Persistence/Runtime/Projections/Drivers + agent/tooling + Projects [S34]. | Use existing domain layers and HTTP control plane. Cut coherent builder/status/result surfaces, not the entire runtime; include admission/result/late-effect proof for touched behavior. |
| Workbench | Razor + many owner modules, FileTools.Desktop, workflows/runtime, conversations shell, Memory and Processes [S35]. | Start with a coherent surface and inventory deferred slots/overlays. Preserve Structure/Gantt/file/agent interactions; do not replace the workbench with a fake global workspace service. |

## Backend-only projects are not automatically missing UI extractions

`CanDoItAll.Modules.Security` and `CanDoItAll.Modules.AgentFramework.ProviderManagement` use the plain .NET SDK, not the Razor SDK [S32, S37]. Find their UI consumers elsewhere. Retain owner implementations and do not manufacture empty rendering libraries/sandboxes for them. `*.Contracts` projects likewise serve a contract role, not a screen.

The current module tree also contains the AgentFramework, Prompts and CrmHr implementation hosts alongside their extracted renderers [S40]. That is intentional in this architecture. “A module still references Infrastructure” does not by itself invalidate extraction of its **separate renderer library**.

## Choosing the next child

Choose based on the smallest useful rendered closure, stable owner contracts, actual current dependency cost and testability. A candidate with few project references may still have a complicated workflow. A large module may have a clean, valuable small surface. Discover the surfaces, estimate the actual cut and measure a baseline before committing to a module order. Do not reopen a validated seam solely to enforce a new naming convention.
