# Target Solution

## Source Of Truth

- `ProcessRun` owns runtime hierarchy: `ParentRunId`, `ParentStepRunId`, `RootRunId`, and `HierarchyDepth`.
- `ProcessStepRun` remains the execution state of the step in the parent process. It does not own a duplicated child status.
- A subprocess step definition owns a subprocess definition reference. It stores `SubprocessDefinitionId` and `SubprocessDefinitionSnapshotName`.
- Reports and UI projections query child runs by `ParentStepRunId` and aggregate by `RootRunId`.
- The child run status is canonical. Parent subprocess step status is updated as a consequence of child state transitions for workflow progression and user readability.

## Runtime Orchestration

- Existing process outbox and dispatch services remain the only scheduling loop.
- When the dispatcher sees a ready subprocess step, it calls a service method that idempotently creates or reuses the child run for that parent step.
- Child runs are normal process runs, so existing step progression, AgentFramework execution tagging, recovery, audit, and outbox behavior still apply.
- No long-lived observer thread is created per subprocess. Observation is query/projection driven, with bounded synchronization during dispatch and read/progress operations.
- Run start rejects missing subprocess targets, unavailable active published versions, direct self-cycles through ancestor definitions, and excessive hierarchy depth.

## Manager Control Plane

- Process versions store an optional manager agent override id and display snapshot.
- Process runs snapshot the chosen manager agent so historic reports remain understandable.
- HR matching uses the override when selecting manager role candidates and records why that selection was made.
- Manager reports are deterministic projections over run tree state: active work, blocked steps, failed children, stale steps, and next recommended intervention.
- Manager instructions are explicit runtime journal/control-plane records. They are never silently swallowed.

## UI And Canvas

- Subprocess nodes use their own visual family, palette, icon, and detail chips.
- The right-click canvas workflow includes adding a subprocess step and changing a subprocess target.
- Step editor exposes a process selector only for subprocess steps.
- Double-clicking a subprocess node opens the referenced process definition in a new browser tab through JS interop.
- Canvas actions remain strongly typed through action id constants.

## Template And Agent Strategy

- Default templates add a `.NET implementation subprocess` that is small enough to run independently.
- Parent software-delivery templates can reference subprocess templates by process key during import.
- Agent seed changes are limited to useful manager or .NET implementation agents that are actually consumed by templates.

## Agent Framework 1.3 Boundary

- MAF 1.3 sub-workflows, handoffs, A2A cards, continuation tokens, and events inform the design.
- CanDoItAll does not persist MAF workflow objects inside process tables.
- AgentFramework execution remains an adapter invoked by process dispatch, with process run/step ids in execution metadata.

## Revalidation Points

- After subbundle 02: verify source of truth and runtime idempotency before manager/UI work.
- After subbundle 04: verify UI actions do not bypass runtime validations.
- After subbundle 06: verify real scenario proof before closure.
