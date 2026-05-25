# Runtime Invariants

## INV-001 Step boundary truth

A process step may only perform operation classes declared by its step contract or derived by a safe explicit default. Heuristic text classification may warn or suggest, but must not silently allow product mutation on an artifact-only or analysis/design step.

## INV-002 Artifact production is not product mutation

Writing a process artifact, architecture record, report, legal note, decision record, business plan, or research summary must be distinguishable from mutating the product/business target.

## INV-003 Process finalizer owns completion

Direct agents, workflow-backed roles, subprocess parent steps, manager recovery, and future executor types must all pass through the same process-owned finalizer before transition.

## INV-004 Recovery lineage is first-class

A recovered artifact must identify both the execution/recovery run that created it and the execution/step it recovers for. Validation must accept recovery lineage explicitly, not by accidental string matching.

## INV-005 No blocked step without unblock path

When the runtime blocks a step because it is waiting for automatically requested upstream materialization, there must be a deterministic event-driven path to unblock or requeue it when the missing artifact appears.

## INV-006 Disposition is not artifact production

Repair/no-go/escalation branches are valid governed outcomes for review/approval/QA disposition steps. They must not mask failure of the current step to produce its own required output artifact.

## INV-007 Storage-backed format validation

Format validation must inspect actual artifact content through managed storage when the contract requires parseable content such as JSON.

## INV-008 Generic core

The process core may mention software examples in tests and skills, but generic runtime contracts must not depend on Blazor, .NET, JavaScript, browser proof, or code-specific assumptions.
