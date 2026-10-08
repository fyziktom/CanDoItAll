# Current continuation map — 2026-10-08

**Review:** `components-decoupling` at `8549e6a18a22638d595bc76ba4240a61ba70351d`, a merge from `development`. This is not an execution pin. Source IDs resolve in [the register](sources.json). No application build, evaluated whole-repo graph or browser was executed by this review.

## Decision table

| Family | Source-backed status | Next action |
| --- | --- | --- |
| Standalone Processes | Confirmed substantial renderer/owner coupling in 8 Razor components; no standalone Processes.UI in the observed `src/UI` inventory [P01–P10] | **Next full assignment: PC1**, including both workspaces and meaningful children |
| Workbench | Maintained WB6 record reports 20 native Razor entries including imports, zero unfinished active renderers; final candidate validation remains qualified/in progress [G05] | Preserve six existing leaf families; verify touched process/workflow consumers after the merge. Do not repeat WB1–WB6 extraction or the expired demo campaign |
| Agents, editor, capability/team authoring, workflow authoring | Existing UI families and successive completion briefs describe A1/A2, PP1/PP2/PP3, CA1/WF1/AC1 progression [H17–H25] | Preserve existing boundaries; rerun affected actual consumers, not an automatic new Agents extraction |
| Workspace Core/API/storage selection/catalog/recovery/Data Sources/configuration | Existing leaf families; closure briefs explicitly distinguish structural completion from historical application failures [H08–H14, N03] | Do not use the v3 map to re-extract them. Current native fixture/closure status must be verified when affected, not inferred from old pending/pass labels |
| Collaboration, TestLab, Plugins, SchedulerPlanner, Memory, Resources | Existing libraries and completed extraction/correction waves [H01–H07] | Protect touched shared lifetimes and consumer journeys; no new blanket module rewrite |
| Projects portfolio and Files | Existing light libraries and P1/P2 progression [H15–H16] | Protect project lifetime, staged selection, file scopes and native save/launch consumers |
| Shared conversation/AppComponents/configuration/Git families | Inspected project files show established rendering/abstraction references, not a demonstrated new heavy module edge [N01–N05] | Evaluate the **consumed** transitive/render/effect closure. Do not call every remaining host/service reference a defect |
| Backend-only modules, dormant compatibility renderers, app composition | Not automatically missing UI extractions | Classify by real reachability and responsibility; do not manufacture a sandbox or delete compatibility code |
| Final whole-repository census | Not executed as a semantic proof by this review | After PC1, run a current route/render/deferred-overlay and evaluated graph census. Record real residuals individually before authorizing another extraction |

“Existing library” or “reported complete in a maintained/historical record” is not this review's independent certification that every behavior is green. The audit's strongest direct conclusion is the remaining standalone Processes boundary. Additional genuinely active renderers discovered during PC1 entry/closure must be listed with paths and ownership, not silently assumed absent.

## Confirmed remaining Processes families

| Current native source | Remaining presentation |
| --- | --- |
| `ProcessWorkspaceShell.razor` | Catalog, full definition editor, all 8 detail tabs, run subsections, status/charts, launch/recovery presentation, manager chat/voice and event/run dialogs |
| `LiveProcessesDashboard.razor` | Activity, Agents, Graphs, Tool history, operator actions, run grouping/hide/restore, details/files and context presentation |
| `ProcessDefinitionCanvasPanel.razor` | Actual CanvasWorkbench, toolbar, gestures and floating toolbox/selection/editor |
| `ProcessDefinitionRoleEditorPanel.razor` | Full role dialog, assignments, workflow preference/raw identifiers, templates and validation |
| `ProcessDefinitionStepEditorPanel.razor` | Basic/operation/routes/roles/artifacts/subprocess mappings and full step selection/editing |
| `ProcessTemplateLibraryPanel.razor` | Browse/filter, source previews, import actions and target selection |
| `ProcessRunFilesDialog.razor` | Real read-only FileBrowser/FileInteraction, authorized download/local-action presentation and lifecycle |
| `ProcessRunCancellationAction.razor` | Cancellation button/status rendering, while native command/outcome ownership remains outside the leaf |

Three native route-host files remain legitimate adapters: ProcessesPage, ProjectProcessesPage and LiveProcessesPage. Their complete files expose `/processes`, `/projects/{ProjectId:guid}/processes`, `/processes/live` and `/projects/{ProjectId:guid}/processes/live` [P12–P14]. Preserve their current query names and semantics; refresh them against the actual execution checkout at entry.

## Order after the demo interruption

1. Reconcile the actual merged checkout and source/sibling pair; reproduce only current relevant failures.
2. Complete PC1 as one staged assignment, including bounded regressions and native/browser proof.
3. Perform residual classification and an appropriate final integration checkpoint. If no active heavy renderer remains, close the architectural wave instead of inventing another module task.
4. Keep separate any unresolved release/demo/live-provider or platform gate. Its existence does not justify pretending the extraction is unfinished, nor does extraction completion waive it.
