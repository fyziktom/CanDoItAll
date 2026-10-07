# Workbench execution presentation

Status: renderer extraction complete under WB6; final candidate validation is in progress.
This record does not claim genuine-model proof or demo readiness.

## Boundary decision

The remaining Workflow and Process presentation belongs in a single
`CanDoItAll.Workbench.Execution.UI` Razor library and an independent
`CanDoItAll.Workbench.Execution.UiSandbox`. The Workbench module remains the native host.
The existing Structure, Planning, Operators, Authoring, Insights and Content boundaries
remain in place. No other module extraction is part of this work.

The leaf renders the Workflow add/input/preview/start forms, Process link/confirmation,
the full staffing workspace and its actual candidate picker, candidate details and switch
confirmation. A Workbench wrapper may adapt native values and callbacks; it must not retain
the extracted form markup or supply a native child through a render fragment. Managed-file
delete disposition completes the existing Content family.

Use immutable presentation snapshots and callbacks captured for the displayed opening.
Only role, candidate, saved definition/version and opening identifiers needed for UI events
cross the boundary. Provider/model names are display values. Routing, credentials, authority,
prepared requests, native services and mutable host sessions remain in the module. A child
result names its original opening and role; it cannot use a later parent parameter after
an await. Close retires the view and does not undo an accepted operation.

Use the existing neutral BaseLib/CanvasLib overlay, record picker and layout components.
Do not introduce a generic workflow engine, transaction coordinator or service locator.
The module maps native launch state to explicit presentation flags; the renderer does not
infer authority from a label or from the presence of an arbitrary object.

## Original Workflow lifetime repair

Each opening captures its project admission, actor, runtime generation, original node and
navigation revision. Each submission has a handler-level busy gate. Option and input reads
also carry a request revision. Completion may update only the original current opening;
the bounded native outcome history retains accepted results after its view retires.

A saved Workflow start keeps its original intent, definition version and accepted run.
Observation uses that admission and does not call Start again. A known rejection can be
corrected; an unknown effect remains locked to observation. Accepted creation retains the
created node even when refreshing the canvas fails. Canonical task attachment keeps its
existing native owner and compensation evidence.

Projected project/task nodes are reconstructed on reads. Their temporary record IDs are
not persistent identity. The parent check compares owner identity and project admission;
persisted nodes additionally retain their record ID. Changing the saved Workflow version,
input settings or original persisted node invalidates a fresh start.

## Proof and closure

Validation must cover actual native owners, stale callbacks, two views, replacement project
lifetimes, duplicate submission and failed observation after acceptance. Renderer tests
exercise the actual child family. The standalone sandbox must publish and run without
Workbench, Process application/runtime, provider execution or persistence dependencies.
Evaluated build references, runtime assemblies, rendered children and asset/watch inputs
are separate checks; unresolved references do not prove isolation.

All browser evidence uses 1920 by 1080 pixels at DPR 1. Keep deterministic evidence separate
from genuine-model runs. Recovery preserves the data-protection keys, storage catalog and
original host-binding identity together with the database. Closure requires the final
native candidate, repeated recovery, signed source commits and the Czech operator runbook.

## Workflow checkpoint

The Workflow add and start forms now render in the Execution library. The native adapter
preserves full input state, uses typed Workflow identities and captures callbacks for each
opening. Independent renderer tests passed 5/5; the native Workflow lifetime suite passed
26/26. Two desktop scenario views preserve independent raw input and observation state.
The sandbox requires Development for the source-assets loop; published assets are validated
separately at final closure. Process and final candidate proof remain in progress.

## Process checkpoint

The leaf owns Process link/confirmation, full staffing and the actual Agent picker, details
and switch children. Existing public display records and dynamic child names retain type
forwarding from Workbench. The native Process start record, authority, source snapshot,
variables, prepared request and admission remain in Workbench and the Process application.

Each nested selection carries its opening, role, candidate and previous candidate identity.
Replacement or disposal cancels only that opening's child dialogs. The parent rejects results
when the role snapshot or opening changed; a second dispatch cannot open another picker.
Read-only details remain available for accepted plans. Native callbacks independently fence
the original actor, runtime, project admission and launch intent. Link receipts retain the
original native result, and failed readback uses observation rather than another mutation.

The unreachable inline staffing branch has been removed from the composition wrapper.
Existing preparation restore, explicit new intent, continuation and graph delivery owners
remain unchanged. The independent host exposes the same child types at /process.

## Final presentation census

The source inventory contains 20 native Razor files, including the imports file. Build
outputs and copied example templates are excluded. Every active renderer has a leaf owner;
the remaining native files are hosts, adapters or retained legacy compatibility surfaces.
There are zero unfinished active renderers and zero unclassified activations in this census.

Paths in the following table are relative to `src/Modules/CanDoItAll.Modules.Workbench`.
All `Pages/Components/ProjectStructure` adapters retain native effect ownership. The leaf
owns its scoped CSS and delegates neutral overlay, selection and layout assets to Components.

| Native source | Actual caller and retained responsibility | Renderer / independent scenario |
|---|---|---|
| `Pages/ProjectStructurePage.razor` | Registered `/projects/{ProjectId}/structure` route; admission, reads, writes and captured openings | Structure workspace; Planning, Insights, Content, Operators, Execution and existing authoring leaves |
| `Pages/ProjectCalendarPage.razor` | Registered `/projects/{ProjectId}/calendar` route; calendar reads, mutations and Agent context | Planning `PlanningCalendarSurface`; Planning sandbox |
| `Pages/ProjectStructureDeferredSurface.razor` | Structure route; render scheduling only, no feature markup | Its already classified child adapters |
| `Pages/Components/ProjectStructure/ProjectManagerSummaryPanel.razor` | Structure Insights slot; native query, admission and state-store adapter | Insights summary surface; Insights sandbox |
| `Pages/Components/ProjectStructure/ProjectStructureGanttPanel.razor` | Structure Planning slot; native task/calendar coordination | Planning Gantt surface; Planning sandbox |
| `Pages/Components/ProjectStructure/ProjectStructureGanttTaskDialog.razor` | Gantt panel and edit coordinator open the actual dynamic wrapper | Planning Gantt task editor; Planning sandbox |
| `Pages/Components/ProjectStructure/ProjectStructureTaskCreateDialog.razor` | Canvas task coordinator opens the actual wrapper; pricing and commit owner retained | Planning structure task editor; Planning sandbox |
| `Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor` | Structure route; pure composition of typed presentation snapshots and captured callbacks | Execution `/workflow`, `/process`; Operators and Content sandboxes |
| `Pages/Components/ProjectStructure/ProjectStructureProcessAssignmentDialog.razor` | Canvas dialog composition; thin mapping and callback adapter | Execution full staffing and all three actual nested children; `/process` |
| `Pages/Components/ProjectStructure/ProjectStructureSupportDialogs.razor` | Structure route; summary/transcript/legacy Mermaid and original delete-prompt mapping | Content analysis and managed-file disposition; `/analysis`, `/deletion` |
| `Pages/Components/ProjectStructure/ProjectStructureFileBrowserWindow.razor` | Structure support window; native storage scope and effect receipts | Content file collection and FileTools interaction children; `/files` |
| `Pages/Components/ProjectStructure/ProjectStructureAttachmentPreviewDialog.razor` | Canvas dialog composition; native composition descriptor and node callbacks | Content file interaction with the registered real renderer; `/files`, `/editors` |
| `Pages/Components/ProjectStructure/ProjectStructureTextAssetCreateDialog.razor` | Text asset coordinator opens the actual dynamic wrapper; original save owner retained | Content text form; Content `/` |
| `Pages/Components/ProjectStructure/ProjectStructureRuntimeLaunchApprovalDialog.razor` | Native runtime launch coordinator opens the actual wrapper and consumes its result | Operators runtime approval; Operators sandbox |
| `Pages/Components/ProjectStructure/ProjectStructureWebPreviewDialog.razor` | Canvas dialog composition; original runtime stop and close callbacks | Operators web preview and EmbeddedBrowser; Operators sandbox |
| `Pages/Components/ProjectStructure/ProjectStructureAgentChatContextProvider.razor` | Structure and Calendar routes; contextual authorization and execution notifications, no visual surface | Existing conversation shell and Agent UI |
| `Components/WorkspaceAgentChatContextProvider.razor` | Web MainLayout; workspace Agent context registration, no visual surface | Existing conversation shell and Agent UI |
| `Components/ProjectEventsCalendar.razor` | Obsolete compatibility adapter; no product caller, route or template activation found | Existing neutral CanvasCalendar; retained for compatibility |
| `Components/ProjectStructureCanvas.razor` | Legacy JS canvas adapter; only self-type references, no product caller, route or template activation found | Legacy `workbenchCanvas` retained, not an active product surface |
| `_Imports.razor` | Razor compilation imports | No renderer |

The dynamic Process picker, details and switch dialogs now resolve to Execution.UI even
under their historical namespaces. `Properties/ExecutionTypeForwards.cs` preserves their
public identities and the safe display records. Native `OverlayStates` keeps project
authority, mutable sessions and prepared requests. Its whole contents were not extracted.
The staffing renderer opens picker/details/switch itself through the neutral DialogService;
the native host cannot inject an old renderer into a content slot.

`WorkbenchModuleServiceCollectionExtensions` retains storage, runtime and application
registrations. Its fenced Markdown component registration resolves the Content-owned
Mermaid renderer through the existing forwarding. `Composition/ModuleAssemblies.cs`
registers the native route assembly. Template and reflection searches found no additional
Workbench visual entry. The dormant `workbenchInterop.js` canvas/calendar compatibility
API remains included by Web; `project-structure-validation-overlay.js` is also retained.
Neither script introduces an unclassified renderer. Existing WB1-WB5 forwarding and leaf
project reference declarations remain unchanged.

The managed-file confirmation renders in Content.UI and emits a typed storage disposition.
The native adapter captures the complete original prompt. Cleanup planning, physical file
ownership, admission, durable receipts and post-commit reconciliation stay with their
existing native services. Display counts do not authorize a deletion.

## Independent proof entry points

Execution.UI and its sandbox resolve through 13 projects: the two new projects, neutral
BaseLib/Common/OverlayLib/CanvasLib, RecordBrowsing, AgentFramework.Models and its five
neutral abstraction dependencies. Runtime-reference traversal and public-contract tests
reject module implementations, EF, provider execution, service-provider bags and credential
models. Negative controls reject forbidden transitive edges and unresolved references.
The three dynamically activated Process child types are asserted to live in the leaf assembly.

The independent Execution tests are selected by Components and Stable solutions and all
three explicit component CI selections. Native focused proof covers Workflow creation and
start, Process staffing/link/launch ownership, and deletion against a successor opening.
The new Content tests cover both managed-file dispositions and retained callback identity.
The final broad checkpoint and production customer journeys are recorded separately.

The completed focused checkpoint passed 58 native cases, 12 independent Execution cases
and five managed-file confirmation cases, with exact discovery and no skipped cases.
Parity and Fast publishes each contained 13 application assemblies; all 61 discovered
assets per publish were served successfully, with matching decoded gzip/identity bytes.
The actual long staffing renderer kept its header fixed while the role rail and card grid
scrolled independently. Accepted plans exposed observation and read-only details.

Bounded watch measurements included three Razor, C# and scoped CSS edits with restored
source hashes. Ordinary method edits and corrected scoped CSS probes retained process and
browser identity. Enum member changes required SDK process restart; the automatic browser
restart page did not always recover, so explicit reload remains a documented recovery step.
One initial Razor restoration missed its observation deadline. The original CSS border
probe was invalid because an inline dialog border overrode it; the corrected outline probe
demonstrated actual computed updates. Original attempts remain in private evidence.

Independent scenario callbacks explicitly request their owner's render after applying
typed results. Browser proof detected and repaired this scenario-host omission before
publish closure; native application callbacks already use their native EventCallback owners.
Final no-write portability enforcement passed with 15,293 reviewed findings. The two new
allowances are reviewed project README discovery text, not new operating-system assumptions.
