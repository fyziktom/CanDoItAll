# WebGL process workbench concept bundle

## Status

This initiative bundle was prepared and executed on **2026-04-20** for a concept branch. It now records the completed implementation and proof trail for the **WebGL process-workbench concept**, shipped as a perspective-first, center-lane 3D sandbox while keeping the production `ProcessWorkspace` out of scope.

## Validation Summary

- Bundle preparation status: `Prepared on 2026-04-20; execution closed on 2026-04-20`
- Bundle readiness gate: `Passed on 2026-04-20`
- Execution status: `Completed on 2026-04-20`
- Subbundle gate review: `Main subbundles 01-10 completed; corrective playbooks not triggered`
- Final closure gate: `Passed on 2026-04-20`
- Browser validation analytics: `Passed on 2026-04-20 with fresh screenshots and semantic proof`

## Why this bundle exists

The current repository already has a strong typed-canvas pattern in `CanDoItAll.Components.CanvasLib`, a dense process authoring surface in `CanDoItAll.Modules.Processes`, real template processes under `Templates/Processes`, and an existing Playwright strategy that relies on semantic canvas helpers rather than raw pointer-only automation.

That combination makes the repository a good place to evaluate a **WebGL-based process workbench with real depth**. The bundle was designed to answer one focused question:

> Can a thin Blazor wrapper over a JS-owned WebGL runtime make dense process diagrams more legible and still remain testable and concept-safe?

## Executive direction

This bundle intentionally **does not** replace the production 2D process workspace. Instead it stages the work in this order:

1. baseline and renderer decision lock,
2. universal WebGL library creation,
3. JS-owned runtime foundation,
4. architecture gate A,
5. process-template-to-scene adapter,
6. dedicated WebGL sandbox project,
7. authoring interactions in sandbox-only in-memory state,
8. architecture gate B,
9. automation bridge and screenshot/semantic proof,
10. final closure and migration rubric.

## Key design decision

The shipped implementation uses a **guided perspective 3D scene** instead of either a flat 2D replica or an uncontrolled free-fly graph editor:

- The main process path stays deterministic and centered.
- Roles and supporting nodes spread to the left and right flanks to reduce overlap.
- Depth is used semantically to stage the route and strengthen legibility.
- The sandbox authoring camera is **perspective-first** with orbit, pan, zoom, and reset controls.
- Labels stay in a **DOM/HTML overlay mirror** for readability and automation.
- Rendering, hit-testing, drag preview, and connection preview stay in **JavaScript**, not in per-frame Blazor logic.

## Representative sandbox templates

| Template | Role | Why it matters |
| --- | --- | --- |
| customer-onboarding | Simple | Fast sanity check for sparse scenes. |
| architecture-decision-governance | Medium | Branching plus governance semantics without maximum density. |
| branching-code-review | Dense | Stress case for overlap, routing, and authoring. |

## Read first

1. `01-executive-summary.md`
2. `02-bundle-intent-and-target-direction.md`
3. `03-current-implementation-audit.md`
4. `requirements/01-normalized-requirements.md`
5. `architecture/01-target-solution.md`
6. `plan/01-phase-plan.md`
7. `spreadsheets/01-user-stories-and-functional-matrix.xlsx`
8. the selected `subbundles/<key>/README.md`
9. `codex/MASTER_TASKS.json`

## Bundle structure

- `inputs/` preserves the raw request and analyzed source artifacts.
- `analysis/` captures repo-backed findings, risks, and the WebGL decision context.
- `inventories/` enumerates hotspots, templates, tests, and proposed project/file additions.
- `requirements/` turns the request into execution-grade requirements and invariants.
- `architecture/` defines the target shape, library boundary, sandbox shape, and proof strategy.
- `plan/` defines the phase sequence, dependency map, and review checkpoints.
- `proof/` defines the required build, screenshot, and semantic proof contract.
- `spreadsheets/` holds the workbook requested by the user.
- `shared-prompts/` gives Codex reusable implementation, review, proof, and corrective prompts.
- `subbundles/` contains the ordered execution slices and corrective playbooks.
- `codex/` contains machine-readable tasks and validation commands.
- `reviews/` captures the completed execution report, architecture gate logs, and closure notes.

## Strict execution rule

If any architecture review gate fails, Codex must:

1. stop all downstream work,
2. execute the mapped corrective subbundle,
3. refresh the blocked proof,
4. rerun the failed gate,
5. continue only after the gate explicitly passes.

No downstream work may proceed on "probably good enough" evidence.

## Final readiness target

The bundle is closure-ready only when:

- all selected subbundles pass their progression gates,
- Gate A and Gate B each have explicit memos,
- any triggered corrective subbundle is completed and linked,
- build and targeted tests pass,
- the dedicated WebGL sandbox has fresh screenshot proof,
- semantic automation proves node move and connection mutation,
- the final migration rubric states whether the concept merits a future pilot.
