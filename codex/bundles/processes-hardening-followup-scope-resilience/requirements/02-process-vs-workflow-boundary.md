# Process vs Workflow Boundary

## Required Interpretation

A workflow may be an executor assigned to a process role, but the workflow is not the process.

The process runtime owns:

- process run state,
- step status,
- role assignment,
- artifact expectations,
- artifact inputs,
- branch outcomes,
- dependencies,
- manager recovery,
- approval and escalation semantics,
- process-owned validation,
- durable handoff records.

The workflow runtime owns:

- internal workflow graph,
- workflow nodes,
- workflow executor state,
- internal workflow tool calls,
- workflow output data.

## Consequences

- A workflow-backed process step must still load `ProcessArtifactExpectation` records from the process step definition.
- Workflow outputs must be mapped to process artifact records with process-owned provenance.
- Process finalizer must validate workflow-backed artifacts the same way it validates direct-agent artifacts.
- A workflow's internal completion state is not enough to transition a process step to completed.
- A process step's branch outcome must be chosen according to process branch definitions, not workflow-local status text.

## Anti-Pattern To Remove

```text
Workflow handled -> workflow status says completed -> process step completed
```

## Required Pattern

```text
Workflow handled
  -> workflow output/artifacts projected or linked into process artifact ledger
  -> process step finalizer validates process artifact contract and scope policy
  -> process disposition router picks completed/branch/block/fail
  -> process transition applied
```
