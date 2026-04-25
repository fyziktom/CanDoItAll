# Subbundles

This directory contains the ordered execution slices and the corrective playbooks used to close the WebGL process-workbench concept.

## Execution order

| # | Key | Title | Depends on | Corrective on failure |
| --- | --- | --- | --- | --- |
| 1 | `01-baseline-and-renderer-decision-lock` | Baseline and renderer decision lock | — | `_corrective-renderer-boundary-reset` |
| 2 | `02-universal-webgl-library-skeleton-and-typed-contracts` | Universal WebGL library skeleton and typed contracts | `01-baseline-and-renderer-decision-lock` | `_corrective-renderer-boundary-reset` |
| 3 | `03-threejs-runtime-foundation-and-host-component` | Three.js runtime foundation and host component | `02-universal-webgl-library-skeleton-and-typed-contracts` | `_corrective-renderer-boundary-reset` |
| 4 | `04-architecture-review-gate-a` | Architecture review gate A | `03-threejs-runtime-foundation-and-host-component` | `_corrective-renderer-boundary-reset` |
| 5 | `05-process-template-projection-and-2_5d-scene-adapter` | Process template projection and center-lane 3D scene adapter | `04-architecture-review-gate-a` | `_corrective-scene-contract-and-layout-reset` |
| 6 | `06-dedicated-webgl-sandbox-and-template-switching` | Dedicated WebGL sandbox and template switching | `05-process-template-projection-and-2_5d-scene-adapter` | `_corrective-scene-contract-and-layout-reset` |
| 7 | `07-authoring-interactions-and-in-memory-edit-model` | Authoring interactions and in-memory edit model | `06-dedicated-webgl-sandbox-and-template-switching` | `_corrective-scene-contract-and-layout-reset` |
| 8 | `08-architecture-review-gate-b` | Architecture review gate B | `07-authoring-interactions-and-in-memory-edit-model` | `_corrective-scene-contract-and-layout-reset` |
| 9 | `09-automation-bridge-and-proof-surface` | Automation bridge and proof surface | `08-architecture-review-gate-b` | `_corrective-automation-and-proof-reset` |
| 10 | `10-final-proof-closure-and-migration-guidance` | Final proof, closure, and migration guidance | `09-automation-bridge-and-proof-surface` | `_corrective-automation-and-proof-reset` |

## Corrective playbooks

| Key | Title | Typical trigger | Purpose |
| --- | --- | --- | --- |
| `_corrective-renderer-boundary-reset` | Corrective renderer-boundary reset | Triggered by gate failure | Repair any failure where the new WebGL library stopped being universal, the runtime boundary drifted into Blazor/server round trips, or the JS/asset strategy became unstable. |
| `_corrective-scene-contract-and-layout-reset` | Corrective scene-contract and layout reset | Triggered by gate failure | Repair failures where the projected process scene became visually confusing, semantically inconsistent with current process IDs/categories, or too coupled to the sandbox. |
| `_corrective-automation-and-proof-reset` | Corrective automation and proof reset | Triggered by gate failure | Repair failures where screenshots were non-deterministic, semantic automation was too weak, or final proof could not verify actual WebGL state changes. |

## Gate discipline

- `04-architecture-review-gate-a` must explicitly pass before process-template projection begins.
- `08-architecture-review-gate-b` must explicitly pass before automation hardening begins.
- Any failed gate or failed proof expectation immediately routes to the mapped corrective playbook.
- Corrective work must refresh the blocked proof and then rerun the blocked gate before downstream work can continue.

## Reading order

1. `README.md` at the bundle root.
2. `plan/01-phase-plan.md`.
3. `codex/MASTER_TASKS.json`.
4. The specific `subbundles/<key>/README.md`.
5. `proof/00-expected-proof-contract.md`.
