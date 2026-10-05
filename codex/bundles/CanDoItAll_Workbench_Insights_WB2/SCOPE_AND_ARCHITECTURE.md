# Scope and architecture

## Complete the useful families, not a wrapper around the old components

| Family | Extracted in WB2 | Stays native |
|---|---|---|
| Manager Summary | Options, large-scope confirmation, progress, errors/stale state, all metrics and charts, latest activity, diagnostics/notes dialog | Scope/preflight/query owners, retained settled data, authorization and native history/plan composition |
| Activity | Complete four-kind filter/table/totals/paging dialog | Actual report queries, Process cursor protocol and native aggregate provenance |
| Selection | Entire none/single/multi selection presentation, action controls, metadata and advanced details | Current graph/selection, admission, mutations, native task/file/runtime/validation owners |
| Object Index | Actual window, search/tree, context menu and offered actions | Authoritative node projection, selection, action catalog and native confirmation/dispatch |
| Signals | Full marker/progress/priority window | Current canonical multi-node mutation and marker-toggle semantics |
| Canvas Health | Full metrics/spotlight window and explicit validation intent | Existing validation behavior and any effects it invokes |
| Detail child | Actual ProjectStructureNodeDetailPreview and relevant safe value projections | Parsing/normalizing native metadata and controlling what may be exposed |

The retained main Structure page must mount these new renderers. A sandbox that
reimplements the same screens or wraps the old product component is not completion.
Perform a caller/descendant/asset census at entry and closure, including dynamic
slots, dialog openings, native task entry and hidden/legacy paths. Do not delete a
public type solely because lexical search found no caller.

## Preferred placement

Use `CanDoItAll.Workbench.Insights.UI` and a corresponding independent UiSandbox.
Reporting and Selection/Support can be cohesive folders within that leaf. Split
only if actual dependency/consumer evidence makes two useful independent units;
do not create a project or service interface per control.

Rendering may depend on actual neutral BaseLib/Charts/CanvasLib/Common contracts,
RecordBrowsing when used, and narrowly needed safe feature values. Evaluate
transitive edges and public-type closure, not only direct csproj text. No native
Workbench, Projects implementation, MAF runtime, Process Application/Projections,
EF, driver, vault or production Web dependency may enter the light renderer.

Prefer safe presentation records and rendered-origin intents for read windows.
A cohesive view contract may be better for the complete selection workspace.
Neither is mandatory everywhere. Element references, focus, temporary menu state,
chart series and transient interaction remain local where appropriate; native
queries/writes and cross-owner policy do not.

`ProjectManagerSummaryContracts.cs` mixes otherwise useful values with Process
cursor and plan-preflight dependencies [S18]. Do not move it wholesale merely to
satisfy one renderer reference. Keep cursor stacks native, project safe preflight
facts, preserve publicly used values/serialization by appropriate forwarding, and
add a feature contracts assembly only where native and UI consumers truly need it.
The large mixed `ProjectWorkbenchModels.cs` stays native. No mass move into SharedKernel.

## Preserve independent roots

Capture evaluated graphs before modification for the completed WB1/Agents/Workflow/
provider/History/Workspace/Projects/Resources and relevant SimpleChats roots.
Preserve their semantic dependency direction. Planning.UI must not depend on
Insights.UI; product hosts compose them. AppComponents and backend foundations do
not learn Workbench Insights semantics. No service locator, dynamically injected
service bag, reflection-based native renderer escape hatch, or blanket namespace
exception in boundary tests.

Contracts moving across assemblies retain public names, API wire behavior and
necessary XML output. WB1 already demonstrated why copied comments without an
emitted XML file are insufficient. Test actual generated API descriptions when
public models move; do not add descriptions by disabling coverage assertions.

## Deliberately outside this slice

Main CanvasWorkbench graph rendering, node authoring dialogs, toolbox creation,
clipboard/import/recomposition, Process assignment/launch/recovery, runtime host
configuration, actual file-browser/attachment-preview internals and PM business
calculation redesign remain outside WB2. Selection buttons still call their real
existing hosts and must work. They are explicit integrations, not fake placeholders.

Do not invent report export, replay, deletion, transcript access, new analytics
metrics, permission models, persistent read models or global cache invalidation.
No API-only migration, database schema migration, module unification or framework/SDK
upgrade is preauthorized by this bundle.
