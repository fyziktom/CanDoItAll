# Execution plan

The bundle must be executed in the listed order. Downstream work is blocked whenever a review gate or validation step fails.

## 01-apply-manifest-audit-and-gap-baseline — Apply-manifest audit and gap baseline

**Purpose:** Prove exactly what the previous bundle claimed should exist in the repository, identify what is actually present, and establish a non-negotiable baseline before any new changes continue.

**Depends on:** None

**Deliverables:**
- Machine-readable application audit against the in-repo apply manifest
- Human-readable gap register with missing-file samples and category counts
- Stop/go decision for materialization work

**Corrective rule:** Create a corrective subbundle immediately. No template, refactor, or QA work may continue until the baseline audit gap is explicitly closed.

## 02-template-pack-materialization — Template-pack materialization

**Purpose:** Materialize the full file-driven process-template pack into the repository so the loader, exporter, projection service, tests, and build outputs all have a real on-disk source of truth.

**Depends on:** 01-apply-manifest-audit-and-gap-baseline

**Deliverables:**
- output/process-template-pack/ with the complete shared/local resource tree
- toolbox role/step seeds and chrome-actions sidecar
- seed-catalog baseline scenarios and framework source records

**Corrective rule:** Create a corrective materialization subbundle that closes missing folders or sidecars before any architecture work resumes.

## 03-process-template-completeness-and-sidecars — Process-template completeness and sidecars

**Purpose:** Re-check every process definition against the now-current module features and remove any historical simplifications that were only retained because the older module lacked the needed capabilities.

**Depends on:** 02-template-pack-materialization

**Deliverables:**
- Detailed process definitions for all bundled templates
- Role, artifact, checklist, validation, prompt, Mermaid, and projection sidecars
- Current-process completeness review evidence

**Corrective rule:** Open a process-specific corrective subbundle for each failing template and do not continue until every process passes review.

## 04-architecture-review-gate-a — Architecture review gate A

**Purpose:** Stop after the baseline audit and materialization work, then perform the first strict senior-architect review before the run invests in deeper refactors.

**Depends on:** 01-apply-manifest-audit-and-gap-baseline, 02-template-pack-materialization, 03-process-template-completeness-and-sidecars

**Deliverables:**
- Architecture review memo A
- Gap register with severity and owner
- Explicit go/no-go decision

**Corrective rule:** Create a corrective subbundle, block the queue, and rerun gate A after the correction lands.

## 05-loader-di-and-path-hardening — Loader, DI, and pack-path hardening

**Purpose:** Harden the template-pack loading path so it is explicit, testable, and aligned with dependency injection instead of hidden static construction paths.

**Depends on:** 04-architecture-review-gate-a

**Deliverables:**
- Plan and implementation tasks for replacing static pack loading shortcuts
- Explicit pack-root configuration strategy
- Regression tests for loader resolution and pack-root overrides

**Corrective rule:** Create a loader-hardening corrective subbundle and stop before SQLite or refactor work continues.

## 06-sqlite-write-path-hardening — SQLite write-path hardening

**Purpose:** Review the process-module database write paths from a SQLite-first perspective, remove risky multi-context patterns, and define the tests needed to catch locking or partial-write regressions.

**Depends on:** 04-architecture-review-gate-a

**Deliverables:**
- SQLite risk register for the process module
- Refactor plan for single-context or explicit-transaction write paths
- Integration tests for import metadata, repeated seeding, and write coordination

**Corrective rule:** Create a SQLite corrective subbundle and do not continue until the write-path risk is reduced to an explicitly accepted level.

## 07-architecture-review-gate-b — Architecture review gate B

**Purpose:** Stop again after loader/DI and SQLite review work so architectural drift is caught before the codebase is split into smaller files.

**Depends on:** 05-loader-di-and-path-hardening, 06-sqlite-write-path-hardening

**Deliverables:**
- Architecture review memo B
- Decision on unresolved DI or transaction debt
- Updated traceability to remaining refactor tasks

**Corrective rule:** Create a corrective subbundle and rerun gate B before any file decomposition begins.

## 08-surface-factory-decomposition — Surface-factory decomposition

**Purpose:** Split the oversized canvas-surface factory into coherent partials or collaborators so node creation, links, ports, chrome, color rules, and coordinate resolution become maintainable.

**Depends on:** 07-architecture-review-gate-b

**Deliverables:**
- Refactor plan and implementation tasks for ProcessCanvasSurfaceFactory
- Smaller files grouped by responsibility
- Regression coverage for definition/run surface output parity

**Corrective rule:** Create a decomposition corrective subbundle and rerun the component tests before continuing.

## 09-workspace-decomposition — Workspace decomposition

**Purpose:** Split the large ProcessWorkspace component code-behind and canvas partial into smaller units so selection, commands, editors, runtime actions, links, and layout concerns stop accumulating in one place.

**Depends on:** 08-surface-factory-decomposition

**Deliverables:**
- ProcessWorkspace partial split plan and implementation tasks
- Smaller code-behind files grouped by lifecycle, definition CRUD, runtime operations, and canvas commands
- Regression coverage for canvas and dialog interactions

**Corrective rule:** Create a workspace corrective subbundle and stop until the UI regression risk is back under control.

## 10-process-service-and-model-decomposition — Process-service and model decomposition

**Purpose:** Break up the oversized process service and large model files into focused files by responsibility, excluding auto-generated migration designer code.

**Depends on:** 09-workspace-decomposition

**Deliverables:**
- Refactor plan for ProcessesService and companion files
- Focused partials or collaborators for listing, reads, persistence, publication, deletion, runtime, validation, and helpers
- Model splits for definition entities, runtime entities, editor DTOs, and view models

**Corrective rule:** Create a service/model corrective subbundle before continuing to the final regression phase.

## 11-regression-net-and-sqlite-tests — Regression net and SQLite-focused tests

**Purpose:** Strengthen the test net so future process-module changes immediately reveal pack drift, loader regressions, sidecar loss, or SQLite-sensitive behavior changes.

**Depends on:** 10-process-service-and-model-decomposition

**Deliverables:**
- Additional unit tests for pack materialization and sidecar parity
- Integration tests for import metadata and SQLite-sensitive paths
- Updated validation scripts and explicit command list

**Corrective rule:** Create a regression corrective subbundle and do not proceed to final review until the gap is closed.

## 12-architecture-review-gate-c — Architecture review gate C

**Purpose:** Perform the final pre-closure architecture review after materialization, hardening, decomposition, and tests have all been addressed.

**Depends on:** 11-regression-net-and-sqlite-tests

**Deliverables:**
- Architecture review memo C
- Closure decision on remaining debt
- Updated traceability matrix and residual-risk statement

**Corrective rule:** Create another corrective subbundle immediately and rerun gate C before final QA.

## 13-final-qa-audit-and-closure — Final QA audit and closure

**Purpose:** Perform the strict final QA inspection, prove the bundle contents are honest and complete, and only then package the final ZIP for delivery.

**Depends on:** 12-architecture-review-gate-c

**Deliverables:**
- Final QA memo with honest execution boundaries
- Validation result and evidence inventory
- Final ZIP package

**Corrective rule:** Create one last corrective subbundle and repeat the final QA audit. Delivery is blocked until the closure memo is honest and complete.