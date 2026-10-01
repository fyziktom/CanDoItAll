# Projects P1 — portfolio and full editor, not all Workbench

## Surface contract

| Current surface | P1 treatment |
|---|---|
| `ProjectsPage.razor` | Retain route, current caller/profile, navigation, data/effect ownership and production composition; replace reusable rendering with the real leaf |
| `ProjectsBoard`, `ProjectPortfolioCards`, `ProjectPortfolioTreeNodeBuilder`, `ProjectsPresentation` | Move actual portfolio/filter/tree/cards rendering and pure presentation helpers |
| `ProjectModalHost` | Move full overview and all five editor steps: Identity, Dates and phases, Stack profile, Linked objects, Review |
| `ProjectHierarchyModal`, `ProjectHierarchyDialogState` | Move read-only parents/subprojects inspection and recursive drill-down rendering; typed original-target navigation intents |
| Package dialog currently inside `ProjectsBoard` | Move its real controls with safe target-option/status records; existing import/export owner and transactional semantics stay in production |
| Deletion progress/warnings and exact retry controls | Move presentation only; preserve exact project/participant/recovery identity and existing retry owner |
| `ProjectFilesPortfolioPane`, `ProjectFilesDialog`, FileTools coordinators | Retain production implementations for P2; compose the active Files region through a typed host-owned slot |
| `ProjectsAgentChatContextProvider` and builder | Retain production context/admission effects; adapt safe view/selection inputs without giving the new renderer those services |
| Workbench Structure/Gantt/Calendar/Process surfaces | Route to existing implementations; no extraction or redesign in this task |

Do not hide missing P1 editor steps behind production slots. The Files slot is explicitly deferred and its handoff is tested in production; it is not evidence that Files itself has been isolated. A sandbox may label this handoff as host-owned, but must not count a placeholder as a real file-browser test. Every in-scope renderer must be shared by production and sandbox. [R10–R13, R17, R22]

## Suggested placement

- Extend `src/Modules/CanDoItAll.Modules.Projects.Contracts` with the small dependency-closed data/enum/intent family needed by this cut.
- Add `src/UI/CanDoItAll.Projects.UI` for actual renderers and appropriate presentation types.
- Add `src/Sandboxes/CanDoItAll.Projects.UiSandbox` using the same leaf and deterministic scenarios.
- Use a separate Presentation assembly only if it genuinely lets production and sandbox share tested state behavior. It is not an interface/project-count requirement.

The existing Contracts project references only SharedKernel. Keep it backend-free and UI-free. UI may use real BaseLib/Common and justified feature-neutral components; every added dependency must follow actual usage and its evaluated transitive graph. [R14]

`ProjectModels.cs` mixes entities/EF mapping, summaries, editable models/enums and the owner service. Move only a coherent pure type closure, preserving namespaces, enum values and public serialization. Do not move the complete file into Contracts. Either reuse safely extracted models or project narrow presentation records where that has genuine boundary value. Never duplicate the persistence entity model. A moved class still needs its real consumer tests; matching type names alone do not prove compatibility. [R15]

`ProjectFileFilterProjection` already defines one bounded filter/projection for Cards and Files. Retain its meaning, bounds, order and fingerprint; share or adapt that fact once. Do not independently recompute a looser file scope or create a second writable project hierarchy. [R19]

## Required graph direction

Production Web → Projects module/adapter → Projects UI → light Projects contracts/shared primitives.
Sandbox → the same Projects UI + deterministic scenario owner.
Production composition alone supplies retained Files UI and current agent-context integration.

The new UI/sandbox must not reference Projects implementation, Workspace, AgentFramework module, Workbench implementation, EF, Infrastructure, provider drivers, file-storage adapters or the production service registration graph. Do not add product references to Foundation, MAF, AppComponents or shared Components. Core/API/Storage/Memory/Resources/CRM/Prompts leaves and sandboxes must not acquire a Projects implementation dependency. Keep the existing legitimate backend-to-Projects contracts consumers intact.

A RenderFragment slot belongs in the UI/view composition contract, not in an otherwise portable domain contracts assembly. Do not use `IServiceProvider`, `object` dictionaries or reflection to smuggle backend services across the seam. Production may continue calling its owners in-process; this is not an API-only conversion. Preserve the existing Processes/Project Structure HTTP control plane.

## Asset boundary

Move owned scoped CSS with its component; examine scope selectors after namespace/assembly changes. Update Tailwind scanning, static web asset/publish inputs and route discovery only as required. Keep actual BaseLib Dialog/TreeView/widgets and current JS. The old modal CSS contains older selector shapes: prove effective desktop styling, do not presume a file copy applies correctly. Preserve unused legacy media behavior without spending this slice redesigning it. [R23]
