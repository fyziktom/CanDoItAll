# 20 Architecture Integration Closure

## Status

- Ready after score-geometry and neuro-cognitive phases and before final `11-validation-and-architecture-closure`.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
Validate that the score-geometry update and neuro-cognitive patch are fully integrated with v2 and do not contradict source truth, Qdrant projection, probing safety, Epistemic Drive, review, distributed compute, mutation authority, or answer-gating rules.

## Covered Inputs

- All imported neuro patch architecture, requirements, diagrams, contracts, validation, traceability, and subbundles.
- Score geometry architecture, requirements, diagram, contracts, validation, traceability, and subbundle.
- Patch apply checklist.
- v2 execution order, review checklist, and test plan.

## Prerequisites

- `01b-score-geometry-driver` and `14-neuro-foundation-claim-evidence-ledger` through `19-metamemory-abstention-calibration` have gate results or owner-approved deferrals.
- Existing v2 project-scoped phases have gate results or owner-approved deferrals.
- Traceability and validation docs are current.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\README.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\plan\01-phase-plan.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\traceability\01-requirement-traceability.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\traceability\02-neuro-patch-traceability.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\26-score-geometry-driver.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\subbundles\01b-score-geometry-driver\README.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\neuro-patch-test-plan.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\reviews\02-neuro-patch-self-review.md

## Deliverables

- Updated README, manifest, reading order, and execution order.
- Updated traceability for FR-039 through FR-054 and NFR-025 through NFR-036.
- Updated validation/test plan and review checklist.
- Updated diagrams README.
- Self-review and execution-report notes.
- Explicit deferred implementation list.

## Dependency Impact

- Final validation closure can trust that score-geometry and neuro-cognitive additions are not dangling appendix docs.
- Implementation agents can execute root subbundles in dependency order without needing the separate patch bundle.
- Any remaining drift between root `subbundles/`, `plan/subbundles/`, README, and manifest is treated as a blocker.

## Validation Depth

- Run prepared-stage bundle validator.
- Audit traceability rows for all new requirements.
- Audit exact source references in new subbundles.
- Audit safety invariants: Qdrant projection only, source truth, probing evidence not direct truth, simulation speculative, replay non-promoting, salience policy-limited, score projections non-authoritative, mutation authority public write boundary.
- Audit plan order against subbundle prerequisites.

## Implementation Steps

1. Verify score-geometry and imported neuro architecture, diagrams, contracts, requirements, validation, and traceability are self-contained in v2.
2. Verify root and plan subbundles are mirrored.
3. Verify execution order and dependency map include neuro phases in the correct prerequisite positions.
4. Verify reviews and execution report record the patch.
5. Regenerate manifest.
6. Run prepared-stage validation.

## Scope Exceptions

- Does not implement product code.
- Does not produce final completed-stage proof because implementation has not started.

## Do Not Do

- Do not mark implemented phases as complete without proof.
- Do not keep patch-only references that force future agents to read `cognitive-memory-neuro-architecture-patch`.
- Do not leave scalar-only scoring references as accepted behavior.
- Do not leave root/plan subbundle order contradictory.
- Do not weaken existing source/projection/probing/governance rules.

## Acceptance Checklist

- V2 bundle is self-contained.
- New requirements map to owning subbundles, including score geometry.
- New diagrams and architecture docs are registered.
- Root and plan subbundles mirror each other.
- Prepared-stage validator passes.
- Product code remains untouched.

## Proof Required

- Bundle validator output.
- Git/file diff summary.
- Self-review record.
- Manifest regenerated.
- Execution report updated.

## Browser Validation Logging

- N/A for architecture closure.
- Verify downstream UI/browser-proof requirements are present in UI-affecting subbundles.

## Progression Gate

- Do not proceed to final `11-validation-and-architecture-closure` until this closure proves score geometry and the neuro patch are integrated, self-contained, and validation-clean.

## Suggested Agent Prompt

Close the score-geometry and neuro-cognitive architecture integration. Validate traceability, phase order, subbundle mirrors, diagrams, contracts, safety invariants, manifest, and prepared-stage bundle readiness without implementing runtime code.
