# Process vs Workflow Boundary Rules

## Definitions

Process:

- Owns process definitions, process runs, step runs, assignments, work briefs, artifact expectations, artifact records, manager directives, recovery decisions, and process transitions.
- Lives in `CanDoItAll.Modules.Processes`.
- Must be the authority for whether a process step can transition to `Completed`, `Blocked`, `Failed`, or another process status.

Workflow:

- Belongs to the Agents / AgentFramework execution side.
- May execute a role assigned by a process.
- May produce execution artifacts, tool receipts, state, or result summaries.
- Must not decide that process artifact expectations are satisfied unless the Processes runtime validates them.

## Hard Boundary Rules

- Do not move `ProcessArtifactExpectation` validation into the workflow module.
- Do not make workflow executors responsible for process step transition rules.
- Do not duplicate artifact expectation logic in both workflow and process code.
- Do pass workflow execution results through the same process-owned finalizer used by direct agents.
- Do treat a workflow-backed role outcome as an executor-neutral completion input.

## Target Flow

```text
Process step claimed
  -> executor selected
     -> direct AgentFramework agent OR workflow-backed role OR subprocess/manual executor
  -> executor-neutral completion outcome
  -> ProcessStepCompletionFinalizer
       -> project artifacts where applicable
       -> reload artifact ledger
       -> validate required expectations
       -> produce diagnostics
       -> invoke manager recovery if eligible
       -> validate recovered artifacts
       -> choose transition
  -> process step transition
```
