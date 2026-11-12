# Processes UI boundary

The global and project Processes and Live Processes routes compose the same renderers
used by the independent [sandbox](../../src/Sandboxes/CanDoItAll.Processes.UiSandbox/README.md).
The three route files remain native hosts. No database schema, runtime protocol, package,
model, or public HTTP contract was changed by this extraction.

## Ownership and renderer census

| Family | Renderer in `CanDoItAll.Processes.UI` | Native owner retained |
| --- | --- | --- |
| Processes workspace | `ProcessWorkspaceSurface` | `ProcessWorkspaceShell`: projection requests, admission, launch preparation/continuation, browser intent, context, chat and voice |
| Live workspace | `LiveProcessesSurface` | `LiveProcessesDashboard`: polling, filters/paging, hidden groups, operator actions, context and file opening |
| Definition canvas | `ProcessDefinitionCanvasPanel` | Native projection client and canvas command service; viewport and floating-window state stay with the renderer |
| Role editor | `ProcessDefinitionRoleEditorPanel` | Native role command service; `ProcessRoleEditorState` belongs to the current workspace opening |
| Step editor | `ProcessDefinitionStepEditorPanel` | Native step command service; `ProcessStepEditorState` belongs to the current workspace opening |
| Template library | `ProcessTemplateLibraryPanel` | Native catalog/import service; pending search and target belong to `ProcessTemplateBrowserState` |
| Run files | `ProcessRunFilesSurface` | `ProcessRunFilesDialog`, file coordinator, scope provider and host action service |
| Run cancellation | `ProcessRunCancellationSurface` | `ProcessRunCancellationAction` and native operator application service |

The workspace and Live contracts expose typed view state and explicit actions, not a
service provider, database entity, runtime driver, or credential-bearing request bag.
The existing Projections → Contracts/Abstractions/Core → SharedKernel graph is reused.
There is no parallel copy of the native process DTO hierarchy.

Deferred role/run/agent/event dialogs, run subsections, statistics, graphs, canvas
windows, and template previews moved with their containing renderers. Template previews
retain their source text and also render Markdown and Mermaid. File presentation uses
the configured FileInteraction composition, including its actual Markdown renderer.
Native file resolution remains authoritative and the viewer remains read-only.

Manager chat uses `ProcessManagerChatSurface` and the existing `AgentChatSurface` and
`AgentActivitySurface`. The native `ChatWorkspacePanel` maps conversation state and
publishes a typed `ChatWorkspaceBinding`; its existing default consumers retain their
attachment picker and navigation. A headless native activity reader publishes activity
for the current operation. Native execution dialogs and voice orchestration retain their
existing owners. The sandbox supplies deterministic presentation at that boundary.

All eight assigned renderer families are extracted. Remaining markup in the native
components composes these renderers, contextual publication and native effect adapters.
It is not a second implementation of the feature's controls.

## State and completion ownership

Definition, role and step drafts outlive Server-tab unmounting. Same-definition reads
preserve dirty input and the original expected version. Conflicting observations are
shown explicitly; Discard adopts the current observation. Accepted submissions reconcile
against a captured draft, preserving edits made after submission. Invalid GUIDs and
numeric input remain raw text until the user fixes or discards them. A new definition,
database profile or project lifetime retires the previous opening's draft state.

File opening, activation, action feedback and cleanup are bound to their originating
operation. Resources detach before awaited disposal. A late error, `finally`, or close
cannot alter a successor, including A → B → A reopening. Cancellation remains an explicit
native action; closing a view does not cancel an accepted run.

Launch and default-feed operations use independent ownership generations. Read refresh
does not create a new launch intent. Same-scope transient read failures retain the last
accepted projection with an explicit retry; denied or changed scope clears it. Live
operator receipts are tied to the opening/profile rather than the independent read
generation, so a concurrent refresh cannot discard an accepted receipt.

The actual Workbench browser journey exposed a native staffing defect: selected workflow
executors were converted into agent overrides. The existing override mapper now preserves
the selected candidate's typed executor kind. The journey verifies a completed native
workflow, retained preparation and the delivered link to the original project node.

## Validation and current limits

The [PC2 execution record](../validation/processes-ui-pc2.md) records the 2026-10-09
mutation/editor corrections, 530 current affected tests, browser/assets, static closure
and signed source checkpoints. Its [native authoring prerequisite](processes-authoring-authority-prerequisite.md)
contains the real-client lifetime diagnosis and next owner/schema scope. Authoring
durability remains blocked. The PC1 results and timings below retain their original
provenance; they are not fresh PC2 results.

The PC1 execution record is under the ignored `.artifacts/pc1-20261008` directory.
Initial source was `6e4a894ddddd6976643caeb8797c47f492728d01` on `components-decoupling`;
Components was `f3745356182444656edfdef56aa827095632d3ea` and FileTools was
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. Builds use SDK 10.0.303 and the isolated
`ProcUiProof` configuration with local sibling source dependencies. The canonical product
and Stable solution builds use Release because the product solution declares only Debug
and Release configurations.

The original 100 component and two browser cases passed. Failing-first controls exposed
draft loss, late file-open errors, launch busy-state ownership, transient projection loss,
and a Live operator receipt lost during a same-scope refresh. Focused native tests cover
164 cases; affected owner and Workbench unit tests cover 238 cases; real launch/workflow/API
integration covers 14 cases. The light renderer/closure selection has 21 cases, including
forbidden transitive and unresolved assembly controls. Final rerun statuses and visual,
asset, performance, broad and static closure are recorded separately in the execution ledger.

The fresh evaluated source-mode closures contain 181 projects for Web, 25 for the renderer,
and 27 for the sandbox, with no missing project references or cycles. Assembly guards also
inspect actual loaded references and public parameter types. The sandbox does not acquire
the native Processes module, application/runtime/persistence or agent execution host.
CodeAnalytics and Components MCP endpoints were unavailable in this execution session;
source, evaluated restore graphs and runtime assembly checks are the recorded substitute,
not a claim of fresh MCP indexing.

The pre-existing definition/role/step/template authoring owners keep accepted snapshots
in instance dictionaries, while `ProcessWorkspaceProjectionClient` uses a fresh request
scope for those operations. A receipt therefore does not establish durable read-back.
The extraction preserves that client and does not invent a persistence layer. PC1's
durable authoring acceptance remains unresolved unless that separate scope is authorized
and implemented. Browser fixture receipts must not be described as native durable saves.

## Development-loop observations

The task measured the same renderer edits in Web and both sandbox modes with SDK 10.0.303.
Each cell below is the median of three visible-change samples in seconds. Startup used a
new watch process with warm build outputs and machine caches; it was not a cold-machine
benchmark. No pre-extraction Web timing was captured.

| Host | Startup | Watched files | Razor | C# | Scoped CSS | Shared canvas JS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Web | 71.30 | 4,893 | 2.89 | 1.51 | 8.51 | 2.57 |
| Sandbox Parity | 24.88 | 1,084 | 1.46 | 2.48 | 1.44 | 2.31 |
| Sandbox Fast | 24.53 | 1,085 | 1.26 | 2.49 | 1.89 | 2.31 |

Razor edits updated the existing document in all hosts. C# draft initialization required
a browser refresh in both sandboxes but hot-updated in Web. Scoped CSS hot-updated in
the sandboxes; two Web samples refreshed and one hot-updated. Shared JS required refresh
everywhere. No sample restarted its process. Classification uses actual process and
browser-document identity, including SDK-initiated refreshes, not only the test harness's
reload calls. The first measurement attempt lacked that document check and is retained
as an earlier attempt rather than the classification authority.

Fast uses an 18,067-byte generated theme; Parity uses the 117,073-byte production theme.
Fast is useful for a smaller development asset set, while published Parity remains the
production-style asset proof. These measurements show a smaller start/watch scope, not
a universal edit-speed improvement. Temporary measurement edits were restored and all
four original/current SHA-256 hashes matched before final builds.
