# Architecture Checkpoints

## After SB01

- Dependency graph: no new project references; Workbench does not reference AgentFramework module.
- Partial-class policy: no new partial and duplicated assignment policy removed from the page/coordinator.
- Testability: resolver and every strategy tested without `ProjectStructurePage` or a full app host.
- Old-owner shrink: cost service is a thin dispatcher; provider branches live in top-level strategies.
- Extension seam: a fake fifth strategy can be selected without editing dispatcher logic.
- Unlock: `SB02` proceeds only if the C# architecture gate is `Pass`.

### Implementation evidence

- The dispatcher now delegates to top-level Person, Agent, Workflow, and Process strategies. Missing and duplicate registrations are explicit failures, not fallback routing.
- `ProjectStructureTaskAssigneeSelectionPolicy` supplies the scalar projection; mixed direct assignments are represented as state rather than rejected as a conflict.
- Workbench does not reference the AgentFramework module. The AgentFramework module supplies its strategy downstream.
- The independent implementation gate rechecked this against the diff and passed. The Projects `TryAddScoped` fallback and Workbench replacement were verified in both registration orders.

## After SB02

- Lifecycle policy is shared and cannot be bypassed by the owned create/update paths.
- Mixed assignment preservation and direct-mutation blocking still pass after pricing changes.
- `Started`, `Completed`, `Cancelled`, and legacy `Unknown` historical cost is protected.
- Unlock: regression/browser closure proceeds only after Behavioral positive and negative cases pass.

### Implementation evidence

- `ProjectStructureTaskApplicationService` owns the shared Gantt/canvas create-edit saga, quote application, direct-assignment revision CAS, and exact compensation boundary.
- `ProjectStructureWorkItemAssignmentRevisionService` and the CRM mutation bridge update direct-assignment metadata and CRM rows under one serializable mutation scope. This applies to every WorkItem; pricing cleanup is task-specific, preserving non-task WorkItem assignment behavior.
- Direct tests cover an A-to-B replacement, callback failure, a competing mutation during the callback, mixed-assignee scalar editing, and rejected mixed direct-assignment replacement.

## Before final closure

- inspect C#/.csproj diff.
- rerun dependency, partial, testability, old-class-shrink, and extension-seam assertions.
- reopen `SB01` if provider branches or duplicate assignment rules returned.
- reopen `SB02` if any owned path persists a stale explicitly `NotStarted` cost or reprices an `Unknown`/historical task.

### Current closure evidence

- Affected Workbench/Web/component/unit/integration builds are clean. Focused Project Structure tests are `107/107`; application/CRM revision tests are `6/6`; details are `8/8`; graph/attachment are `9/9`; serializable locking is `1/1`; unit policy tests are `48/48`; current-source PostgreSQL HTTP boundary is `1/1` in `17s`.
- The HTTP proof ran outside the sandbox after synchronizing the already clean-built isolated current Web DLL into the integration output. Its earlier stale `07:10` Web-binary run is excluded. The broad dependency-graph build is also excluded because an existing user Web host locked normal output and unrelated external repository `obj` paths were read-only.
- The rendered PostgreSQL-backed Gantt dialog opened a real mixed Person + Agent task at `1600x1000`; the warning was visible, task fields and Save were enabled, and direct assignment changes remained protected. The dialog was closed without save to avoid mutating developer data.
- Independent final C# architecture gate: passed with non-blocking follow-up. The narrow bridge should remain bounded; add focused pricing/revision assertions before changing bulk delete/move paths.
