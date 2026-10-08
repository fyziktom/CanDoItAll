# Review and next-slice decision

## Baseline and coverage

The reviewed branch HEAD was `8549e6a18a22638d595bc76ba4240a61ba70351d` (2026-10-08T19:48:01Z), a merge from development. The review read current governing documents, all 31 historical assignment entry briefs, the maintained WB6 record, selected Process production/test files and directory inventories. [SOURCES.json](SOURCES.json) lists exact coverage. Some large files were read in relevant ranges, not line-for-line end to end; native service implementations marked metadata-only still require full executor inspection. No .NET build, test discovery, PostgreSQL, browser or watch execution occurred in the review environment.

The current user instruction resumes decoupling after the demo interruption. The old WB6 “finish before demo; do not start standalone Processes” scope is historical, not a permanent prohibition on this new assignment. Its tested fixes and native integration remain protected.

## Why Processes, not another Workbench extraction

The maintained Workbench execution record [G05] describes all active Workbench rendering as extracted and explicitly separates final candidate validation. Repeating WB1–WB6 would duplicate completed boundaries. Existing Agents/Providers/Workspace/Projects and other UI families likewise cannot be called missing because the September shared-v3 map still lists them.

By contrast, standalone Processes still combines a large native module with eight Razor renderer families [P01–P10]. ProcessWorkspaceShell and LiveProcessesDashboard render substantial UI while owning reads, drafts, route/context handling, launch/operator effects and browser state. The Process module directly references application/runtime/persistence and agent services. A renderer referencing that module inherits the wrong boundary; the native module retaining these references is not itself a defect.

The next coherent assignment is the full standalone Process family. Its authoring, run/live, files and manager-chat parts share selected definition/run context and lifecycle. Small isolated panel moves would leave most development cost and the same state/effect risks. PC1 instead uses buildable stages within one complete scope.

## Findings and confidence

| ID | Finding | Evidence and confidence | Required treatment |
| --- | --- | --- | --- |
| R1 | Same-definition projection refresh can overwrite unsaved fields | P02: detail-tab change calls LoadAsync; accepted load calls SyncDefinitionEditor; that method assigns editor fields without a dirty/submission reconciliation guard. Source-supported path, not runtime reproduced here | Failing-first real tab/refresh test; stable draft and explicit reconciliation |
| R2 | File-dialog error completion is not fenced like its success path | P08: OpenAsync, ActivateAsync and ExecuteFileActionAsync guard accepted results but assign openError/activationError unconditionally in general catches | Force cancellation-ignoring late errors after retarget/dispose; scope errors, feedback and logging to the originating operation |
| R3 | Awaited file cleanup may retire replacement state | P08: ResetAsync awaits disposal before clearing shared interaction/workspace fields | Reproduce with held asynchronous disposal and a successor open; detach old resources before awaiting and preserve cleanup ownership |
| R4 | Launch/feed/new-intent paths need a whole-lifecycle origin audit | P02: launch/feed finally blocks affect shared busy state; explicit new-intent clears mutable state after awaited storage cleanup | Ordering hypothesis. Demonstrate current behavior before repair; preserve accepted native intent and authority |
| R5 | Failed same-scope refresh removes the complete accepted shell | P02: LoadAsync catch clears shell | Improve lane-specific stale/read-error behavior without retaining denied or other-scope data |
| R6 | Role editor synchronizes local fields on version/selection changes | P05: OnParametersSet/SyncSelectedRole; real server tabs can also unmount editors | Prove dirty/raw GUID/validation survival under the chosen policy; do not lose later edits after a save |
| R7 | Existing harness has implementation-coupled and readiness-sensitive assertions | T01 private-field/reflection access and some synchronous clicks; T05 older selectors, explicit waits and direct DOM click evaluation | Reconcile at entry; keep behavior/theory rows, strengthen real interaction, never delete the obligation |

These are not seven independently reproduced production bugs. R1/R2 have direct source traces; R3/R4 are adverse-ordering hypotheses; R5/R6 are scoped state risks; R7 is validation engineering. A current fix already present earns a verified control, not a gratuitous rewrite.

## Good behavior to retain

P09/T03 already distinguish cancellation requested versus cancelled, prevent duplicate dispatch, avoid cancelling the native command on view retirement, validate receipt identity, and keep a known commit when refresh fails. P02/T02 retain browser caller intent/preparation and restore an accepted launch without re-admitting a new project lifetime. P10 captures project admission, validates current lifetime after projection work and resets inappropriate cached runtime query state. The new seams must preserve these semantics, not flatten them into generic success/error flags.

The reviewed Process projection graph is materially lighter than the native module: Projections references Contracts, Abstractions and Core; Core references Contracts and Abstractions; Abstractions references Contracts; Contracts references SharedKernel [P11/P19–P21]. This is a reason to evaluate and reuse the existing closure, not proof that every public value or evaluated dependency is suitable. No automatic second DTO hierarchy is required.

## Remaining program status

Use the shared [continuation map](../UI_Decoupling_Shared_Bundle/audit/module-map.md). PC1 closes the confirmed large remaining standalone family, not an independently verified assertion that every repository edge is already clean. A final current route/render/overlay/evaluated-reference census must classify any additional active leftovers. Native hosts and dormant compatibility components do not count as unfinished merely because they remain outside src/UI.
