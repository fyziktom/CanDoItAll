# Executive summary

## Assessment

The current `Processes` module has a good functional baseline and several promising architectural seams, but it is still carrying a risky combination of **dual representations**, **destructive persistence**, **oversized orchestration points**, and **small-data read assumptions**.

The most serious issue is not any single bug. It is the fact that several foundational concepts still have more than one effective source of truth. That weakens correctness, testing, long-term maintenance, and confidence in future changes.

## What is already good

- The module is properly registered through existing composition patterns.
- The project-bridge seam is directionally good and avoids stronger compile-time coupling than necessary.
- There is already meaningful integration, component, and MCP test coverage around authoring, publishing, runtime transitions, imports, templates, and canvas rendering.
- `DeleteAsync` in the publication flow already demonstrates the correct direction for explicit transaction use and bulk cleanup.

## What must be corrected before more growth

1. **Canonical dependency model**
   - `ProcessStepDefinition` still carries legacy dependency fields while also storing dependency rows.
   - Multiple helpers reconstruct or synchronize dependency meaning from both representations.
   - Validation currently normalizes and mutates state, which makes the model harder to reason about.

2. **Atomicity and concurrency**
   - `SaveAsync`, `PublishAsync`, and `TransitionStepAsync` lack robust optimistic concurrency protection.
   - Version and slug generation rely on pre-check patterns that are race-prone.
   - The save flow uses intermediate `SaveChangesAsync` calls without a full explicit transaction boundary.

3. **Persistence stability**
   - Definition children are deleted and recreated rather than updated differentially.
   - Stable child identity is lost across saves.
   - This increases DB churn, weakens auditability, and raises long-term merge risk.

4. **Maintainability**
   - `ProcessesService` is still a logical god service despite partial-file splitting.
   - `ProcessWorkspace` is a large stateful monolith spread across several very large partial files.
   - Several template/helper concerns are duplicated across multiple services and even across modules.

5. **Performance and read-shape**
   - Definition listings and analytics still use broad in-memory aggregation patterns that will degrade as data grows.
   - The module needs explicit query services and slimmer projections.

## Headline findings

- Non-canonical dependency representation in:
  - `src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs`
  - `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs`
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs`
- Destructive save pipeline in:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- Publish/version race windows in:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs`
- Runtime orchestration hotspot in:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- Cross-module duplication in:
  - `src/CanDoItAll.Modules.Processes/ProcessTemplate*`
  - `src/CanDoItAll.Modules.Factory/PromptLibraryPackLoader.cs`
  - `src/CanDoItAll.Modules.Projects/ProjectModels.cs`

## Executive verdict

This is a **stabilization and hardening initiative**, not a cosmetic refactor.

The correct next step is to execute the phased remediation plan in `08-codex-execution-plan.md`, beginning with the behavioral baseline and canonical dependency repair. Every later phase depends on that foundation being right.

## Immediate must-fix order

1. Canonical dependency model and compatibility boundary.
2. Pure validation and explicit normalization.
3. Transaction and optimistic concurrency hardening.
4. Differential graph persistence.
5. Publication/versioning decomposition.
6. Runtime state-machine extraction.
7. Query/read-side hardening.
8. Template/shared-infrastructure consolidation.
9. Workspace decomposition.
10. Final schema hygiene and regression closure.
