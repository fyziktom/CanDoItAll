# Task 06 – Fix managed artifact materialization/acceptance ordering

## Problem

The incident artifact contains:

```text
## Runtime Validated Structured Outcome
The process runtime appended this section after validating the structured process step outcome.
Status: Completed
```

But the same step was later rejected by product completion gates and `ProducedArtifactsJson` is empty. This creates confusing evidence: a physically written markdown says `Completed` and `Runtime Validated`, while runtime state says `Blocked`.

The flow in `AgentFrameworkProcessExecutionAdapter.cs` materializes/appends the managed artifact before calling `ToAdapterResult`, which runs product gates.

## Implementation

1. Distinguish three states:
   - structured finalizer output is schema-valid,
   - managed artifact was staged/written,
   - completion gates accepted and artifact slots were produced.
2. Do not append text that implies full runtime acceptance before completion gates pass.
3. Options:
   - stage artifact append first, then promote/append acceptance section only after gates pass,
   - or append a neutral `Structured Outcome Captured` section, then later `Completion Gates Accepted`,
   - on rejection, append optional `Runtime Rejected Structured Outcome` with diagnostics.
4. Ensure `ProducedArtifactsJson` remains empty until gates pass.
5. Ensure parent bridge never treats staged/rejected artifact as accepted output.

## Suggested text changes

Replace:

```text
Runtime Validated Structured Outcome
```

with one of:

```text
Runtime Captured Structured Outcome
```

before gates, and append later:

```text
Runtime Accepted Completion Gates
```

only after gates pass.

## Acceptance criteria

For a false Completed output:

- artifact may exist as staged/rejected evidence,
- receipt must clearly state gates rejected it,
- produced artifact slot is not emitted,
- parent bridge does not accept it,
- operator UI is not misled by `Runtime Validated` wording.

## Regression tests

```text
ManagedArtifactMaterialization_does_not_label_rejected_completed_output_as_gate_accepted
ManagedArtifactMaterialization_promotes_artifact_only_after_completion_gates_pass
RejectedCompletedArtifact_can_be_used_as_rework_context_but_not_as_produced_slot
```
