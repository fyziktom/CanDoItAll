# User story to proof map

| Story ID | Owning subbundle | Acceptance summary |
| --- | --- | --- |
| US-01 | 02-universal-webgl-library-skeleton-and-typed-contracts | The library references BaseLib/Common only and exposes generic scene contracts. |
| US-02 | 03-threejs-runtime-foundation-and-host-component | Architecture docs and runtime implementation keep per-frame work in JS and .NET only receives coarse events. |
| US-03 | 06-dedicated-webgl-sandbox-and-template-switching | Sandbox offers overview and focused views with readable labels and screenshot proof. |
| US-04 | 06-dedicated-webgl-sandbox-and-template-switching | Sandbox template selector loads representative templates without restarting the app. |
| US-05 | 06-dedicated-webgl-sandbox-and-template-switching | Sandbox includes camera/view presets and the default is perspective. |
| US-06 | 07-authoring-interactions-and-in-memory-edit-model | Dragging updates the in-memory scene and the last command summary reflects the move. |
| US-07 | 07-authoring-interactions-and-in-memory-edit-model | A semantic connect/disconnect flow exists and updates the in-memory model. |
| US-08 | 07-authoring-interactions-and-in-memory-edit-model | Selecting a node highlights it and shows metadata/semantics in a side panel or floating panel. |
| US-09 | 07-authoring-interactions-and-in-memory-edit-model | Sandbox can reset the in-memory session back to the selected template projection. |
| US-10 | 09-automation-bridge-and-proof-surface | Runtime exposes stable global helpers and host state with deterministic node/port lookup. |
| US-11 | 09-automation-bridge-and-proof-surface | DOM mirror layer exists with stable data attributes and accessibility labels. |
| US-12 | 09-automation-bridge-and-proof-surface | Runtime provides `exportImageData` or equivalent with deterministic output. |
| US-13 | 01-baseline-and-renderer-decision-lock | No production process route replacement is required in the concept branch. |
| US-14 | 04-architecture-review-gate-a | Bundle contains mandatory Gate A, Gate B, and final closure gate instructions. |
| US-15 | _corrective-renderer-boundary-reset | Corrective subbundles exist and are wired into plan failure paths. |
| US-16 | 01-baseline-and-renderer-decision-lock | Workbook links stories/features to subbundles and proof expectations. |
| US-17 | 10-final-proof-closure-and-migration-guidance | Final guidance defines pilot entry criteria and rollback triggers. |
| US-18 | 02-universal-webgl-library-skeleton-and-typed-contracts | JS runtime assets are committed and build steps are documented under repository tooling. |

## Proof reminder

Stories that involve mutation (`US-06`, `US-07`, `US-10`, `US-11`, `US-12`) require semantic assertions in addition to screenshots.
