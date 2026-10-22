# CanDoItAll.Processes.Application

Coordinates process launch, assignments, launch variables, blocked-run recovery,
automation dispatch, auditing, Git integration, and application-level policies.

This is the process use-case boundary. Domain transitions remain in Core/Runtime and
persistence remains in the persistence project.

Prepared launches retain one immutable plan, source authority, target lifetime and caller intent. The persistence owner commits initial run state and admission together. Data-only ports provide current source leases, target validation and Structure delivery; this project does not depend on product contexts or components.

Acceptance, continuation, delivery and execution completion are separate results. A lost acknowledgement preserves the accepted run identity and exact exception internally; recovery reads the owner receipt and retries only outstanding continuation or delivery. Pure observation does not execute a launch. Older callers without a caller intent retain intentional-repeat behavior, without a safe-replay claim for an unacknowledged request.

The live history window limits finished runs, not quiet active runs or runs awaiting
attention. An explicitly selected run is loaded by identity even outside the recent
list and its size limit; project lifetime checks still apply. Event history keeps its
requested time range. A URL pointing at an older run must show that run's current state.

Live agent and operator-action reads keep the run/step and lower activity bounds but
allow updates after the request began. Execution rows change while these reads run;
an upper timestamp captured before the read can hide a busy agent. Historical
run-record and timeline queries retain their own bounded windows.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Application\CanDoItAll.Processes.Application.csproj
```

## Decision publication condition

`IProcessDecisionPublicationFence` checks the selected generation and enrolled child
inside the effect owner's transaction. It is only the decision condition: the actual
source, profile, manager action or dispatch claim and supported execution contract must
also authorize the operation. A historical child observation is not publication authority.

`IProcessDecisionMaterialReader` retrieves one original material by its full immutable
scope and reference. It does not select the active parent generation or grant execution
authority. Missing material is an explicit recovery failure; callers must not substitute
current source reads.

## Terminal decision accounting

`ProcessDecisionFinalizationCoordinator` closes an empty or already-accounted generation
after its business step becomes terminal. The caller supplies the original action ID,
explicit actor and exact decision/budget expectations. Retain that request before an
uncertain commit and replay it unchanged; a newer displayed expectation is another action.
The coordinator checks the original receipt before current-state selection and does not
change business outcomes or produce effects. Actual child work must first be closed and
settled by its owner. Shared scope closure and durable root recovery are separate owners.
