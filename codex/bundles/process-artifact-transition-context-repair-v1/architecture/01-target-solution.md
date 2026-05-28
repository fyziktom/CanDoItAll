# Target Solution

## Runtime Change

Add internal artifact-validation context to the transition request used by process automation. The context should include:

- executor kind
- execution run id
- workflow run id
- subprocess run id
- recovery execution run id
- recovered-for execution run id

`FinalizeStepCompletionAsync` remains the owner of process-owned artifact validation and produces the context that matched its last validation pass. `ApplyFinalizedStepTransitionAsync` forwards that context to `TransitionStepAsync`. Public API callers do not receive setters for this context, so API/manual completion keeps the manual validation path.

## Boundary Rules

- `ProcessCompletionArtifactValidator` stays the single artifact contract validator.
- `ProcessesService.TransitionStepAsync` keeps revalidating required artifacts before completion.
- Transition request context is internal to the process module and is not a public user contract.
- Template files should change only if the evidence shows template constraints caused the failure.

## Expected Behavior

- Agent-produced current artifacts complete when lineage matches the process-owned execution context.
- Manual stale artifacts still fail with `StaleOrWrongRun`.
- Blazor delivery can move from contract resolution to the build step for a generic Blazor WASM PWA request.

