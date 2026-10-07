# Complete remaining Workbench scope

The final scope is **all active Workbench UI**, not all implementation code and not the
standalone Processes product UI. Current WB1-WB5 renderers remain intact.

## Mandatory remaining family

| Current source/activation | Final treatment |
|---|---|
| `Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor` | Move full Workflow add/start and Process link/confirm/start/review rendering into the new leaf. Preserve already-composed Operators children. |
| `ProjectStructureProcessAssignmentDialog.razor` | Complete full-screen staffing overview, roles, selected candidates, estimates, matching confirmation, review and action states. |
| `ProjectStructureProcessAgentPickerDialog.razor` | Real candidate filtering, selection and nested details, tied to original role/opening. |
| `ProjectStructureProcessAgentDetailsDialog.razor` | Full read-only metadata/capability presentation without runtime services or secrets. |
| `ProjectStructureProcessAgentSwitchConfirmationDialog.razor` | Exact old/new assignment confirmation; no generic true result bound to a new parent. |
| `ProjectStructureSupportDialogs.razor` | Extract remaining managed-attachment delete/storage-disposition presentation; preserve exact native prompt/cleanup semantics. |
| `ProjectStructurePage.WorkflowNodes.cs` | Native options/input/status/creation/start orchestration remains host-owned; repair lifetime and truthful outcomes. |
| `ProjectStructurePage.Processes.cs` and `.ProcessPreparations.cs` | Native preparation, matching, actor/target authority, admission/delivery, restore and status remain host-owned; present through small explicit seam. |
| `ProjectStructurePage.OverlayStates.cs` | Separate UI-safe values from host-only authority/prepared requests; do not move the mixed file wholesale. |

Source list is current at review [S08-S16]. Include corresponding `.razor.css`, helpers,
registration, assembly-qualified names, Tailwind content, scripts and actual nested dialog
activations. Extracted UI must not instantiate the old whole component through a fragment.

## Final census procedure

Enumerate `src/Modules/CanDoItAll.Modules.Workbench`, relevant `src/UI/CanDoItAll.Workbench.*`,
MAF/shared children, Web routes, DI registrations, `DialogService.OpenAsync<T>`, reflection,
template activation and static-web-asset usage. Search all call sites, not filenames alone.
For each row record: source, actual caller, renderer owner, native owner, asset owner,
independent scenario, native scenario, and one classification:
- completed renderer;
- legitimate route/effect/authority or compatibility host;
- developer-only/legacy with explicit evidence of no active product entry;
- unfinished active renderer;
- unclassified (must reach zero).

Read the prior WB5 census if present, but regenerate against final source. Do not call all
native Razor unfinished; do not call all wrappers complete without inspecting their children.
A header, storage-disposition confirmation or dynamically opened picker is still UI.
No dynamic/reflection entry may be removed merely because text search found no static caller.

Acceptance: no unfinished active Workbench renderer and no unclassified active entry;
all prior WB1-WB5 and new family graphs remain correctly directed, actual native host is
runnable, mandatory application flows pass, remaining unrelated legacy is documented.

## Explicit exclusions after this closure

No standalone Processes catalog/definition/monitoring refactor, no other module's decoupling,
no global framework rewrite, no speculative UI redesign, no database upgrade/migration,
no mobile/tablet tuning. Existing Process product services/UI may be used for real rehearsal.
Newly exposed small integration defects may be fixed with tests; broad new features wait.
