# WB1 scope and dependency architecture

## Selected useful family

| Surface | Required move | Original native responsibility |
|---|---|---|
| `/projects/{ProjectId}/calendar` | Header, loading/unavailable/error/retry, actual Calendar, selected event detail, view controls and native list export | Route identity, canonical projection, exact project admission, state writes, artifact navigation and agent context |
| Calendar demonstrations | Real neutral specimen children with explicitly synthetic data in the planning sandbox | No new production CRUD capability or fake persistence |
| Gantt panel | Complete chart/drag source, toolbar/metrics, warnings, all edit intents and export dialog | Projection assembly, current schedules/assignments, final mutation checks, native task/row-order writers |
| Gantt task create/edit | Every existing form field and actual common children | Opened task/assignment snapshot, create/edit pricing/attachment stages, current authorization |
| General Structure task create/edit | Every current form field and common children; preserve distinct create rules | Existing CanvasTask coordinator, repository/native graph context and original transaction/compensation paths |
| Common task editors | Estimate, execution, resource picker, price preview and their input/state policies | Rates, cost provenance, resource availability and committed cost |

Also inspect the obsolete ProjectEventsCalendar wrapper. Preserve compatible public consumers;
do not treat its filename as proof of an additional live surface.

## Intended placement

Prefer `src/UI/CanDoItAll.Workbench.Planning.UI` and a standalone matching `UiSandbox`.
Use one cohesive contract shape per responsibility: immutable presentation plus typed intent
for read-heavy regions; a bounded editor/view contract where copying the full draft on every
render would duplicate authority. Use stable occurrence identities, not row positions or
translated labels. The live draft and its EditContext are owned once per original editor.

A narrow `src/Modules/CanDoItAll.Modules.Workbench.Contracts` or `Planning.Contracts` can hold
truly shared passive values and ports. It must not contain EF types, registrations, arbitrary
factory delegates or a giant façade of Workbench services. Preserve existing public namespace
and wire behavior when relocating a useful data type; inspect serializer/reflection/type-forwarding
consumers before deciding relocation is safe. Small UI projections are often preferable.

The existing ProjectWorkbenchModels.cs is a mixed entity/configuration/service file (S18).
Moving it or native ProjectStructureSurface wholesale just to satisfy a compiler defeats
this assignment. Keep persistence and operation authority native. Do not expose
ProjectStructureAgentContext or a service locator in renderer parameters. An origin token
is not permission; native adapters bind it to the already captured real admission.

Share pure, meaningful policy only where actual runtime and UI consume the same rule;
do not create two pricing/validation implementations. Existing Components.Gantt and
CanvasCalendar types can be used when their evaluated graph is light and their values are
safe. Component-library names alone do not establish either safety or excessive coupling.

## Integration and protected boundaries

The Workbench module composes the new leaf into the existing Calendar route and Gantt/Task
entry points in ProjectStructurePage. Keep those native host contracts compatible. The large
Structure canvas is not copied into the new sandbox. Use a typed seam for the actual planning
observation and accepted completion; do not open another module through a renderer-built URL.

Record before/after evaluated references, not a grep count. No cycles, unresolved references,
UI-to-implementation edges, Foundation/MAF-to-product-UI back edges, or new Workbench edges in
completed Agents, WorkflowAuthoring, Providers, Workspace, Projects and their sandbox roots.
A justified separate Calendar/Gantt leaf is allowed if it preserves these guarantees, but
there is no quota or desired project count. Native hosts legitimately keep services and effects.

## Explicitly outside WB1

The main Structure canvas and toolbox, manager summary/activity, process/workflow assignment
and launch, file/browser/storage recovery, runtime/terminal/media generation, and Processes.
Do not delete them or replace them by sandbox placeholders. Keep all existing production
consumers working. Resource selection/attachment inside task dialogs is in scope; authoring
or launching the selected Workflow/Process is not. No migration, database switch mechanism,
new task entity, API-only rewrite, scheduling engine or generic durable-operation framework.
