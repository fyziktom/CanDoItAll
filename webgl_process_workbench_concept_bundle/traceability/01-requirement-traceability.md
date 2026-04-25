# Requirement traceability

| Requirement ID | Owning subbundle | Raw note anchors | Acceptance signal |
| --- | --- | --- | --- |
| RQ-01 | 01-baseline-and-renderer-decision-lock | IN-01, IN-02, IN-20 | Bundle contains `inputs/00-original-request.md`, `inputs/01-source-artifacts.md`, and `inputs/02-structured-input.md`. |
| RQ-02 | 01-baseline-and-renderer-decision-lock | IN-01, IN-02, IN-15, IN-16 | Bundle layout matches initiative conventions and passes the prepared-stage validator. |
| RQ-03 | 01-baseline-and-renderer-decision-lock | IN-03, IN-04 | Target-solution docs name the rendering boundary, engine choice, and non-goals. |
| RQ-04 | 03-threejs-runtime-foundation-and-host-component | IN-03, IN-05, IN-11 | Architecture rules ban per-frame C# rendering or server round trips during pointer movement. |
| RQ-05 | 02-universal-webgl-library-skeleton-and-typed-contracts | IN-05, IN-06 | New library compiles independently and only exposes generic contracts. |
| RQ-06 | 02-universal-webgl-library-skeleton-and-typed-contracts | IN-05, IN-06, IN-17 | Library defines scene, node, edge, camera, UI-state, diagnostics, and event DTOs. |
| RQ-07 | 03-threejs-runtime-foundation-and-host-component | IN-05, IN-13 | Runtime API and component surface include these controls with typed options. |
| RQ-08 | 07-authoring-interactions-and-in-memory-edit-model | IN-10, IN-13 | Runtime emits semantic events and sandbox can update its in-memory model. |
| RQ-09 | 05-process-template-projection-and-2_5d-scene-adapter | IN-03, IN-04 | Architecture docs define a centered main lane, role spread, semantic Z-depth, and a perspective default camera. |
| RQ-10 | 03-threejs-runtime-foundation-and-host-component | IN-12, IN-13, IN-19 | Library includes a label/accessibility/automation mirror layer with stable IDs and coordinates. |
| RQ-11 | 06-dedicated-webgl-sandbox-and-template-switching | IN-07 | Solution includes a dedicated sandbox project with its own routes and startup. |
| RQ-12 | 05-process-template-projection-and-2_5d-scene-adapter | IN-08, IN-17, IN-18 | Sandbox uses current template pack + projection services and can show at least three representative templates. |
| RQ-13 | 06-dedicated-webgl-sandbox-and-template-switching | IN-09, IN-18 | UI contains template selector and view/camera controls with deterministic state. |
| RQ-14 | 07-authoring-interactions-and-in-memory-edit-model | IN-10, IN-11 | No production persistence or `ProcessWorkspace` replacement is required in the concept sandbox. |
| RQ-15 | 09-automation-bridge-and-proof-surface | IN-12, IN-13, IN-19 | Runtime exposes global automation methods and a debug state object analogous to the current canvas runtime. |
| RQ-16 | 09-automation-bridge-and-proof-surface | IN-12, IN-19 | Proof contract includes both screenshot capture and semantic assertions for node move/connect flows. |
| RQ-17 | 04-architecture-review-gate-a | IN-15, IN-16 | Plan includes Gate A, Gate B, and final closure gates with go/no-go decisions. |
| RQ-18 | _corrective-renderer-boundary-reset | IN-16 | Corrective playbooks are present and referenced by failure paths. |
| RQ-19 | 05-process-template-projection-and-2_5d-scene-adapter | IN-17 | Process adapter reuses template projection services and `ProcessCanvasCatalog` semantics. |
| RQ-20 | 10-final-proof-closure-and-migration-guidance | IN-12, IN-15 | Bundle contains `codex/VALIDATION_COMMANDS.md` and proof docs with exact commands. |
| RQ-21 | 01-baseline-and-renderer-decision-lock | IN-14 | Workbook exists in `spreadsheets/` and is referenced by requirements and traceability docs. |
| RQ-22 | 02-universal-webgl-library-skeleton-and-typed-contracts | IN-05, IN-11 | Library uses committed static assets and an explicit build path under repository tooling. |
| RQ-23 | 10-final-proof-closure-and-migration-guidance | IN-04, IN-11, IN-20 | Final guidance document defines pilot-entry criteria, out-of-scope items, and rollback triggers. |

## Traceability rule

If execution materially changes scope, the workbook and this table must be updated together.
