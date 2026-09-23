# CanDoItAll.Processes.Application

Coordinates process launch, assignments, launch variables, blocked-run recovery,
automation dispatch, auditing, Git integration, and application-level policies.

This is the process use-case boundary. Domain transitions remain in Core/Runtime and
persistence remains in the persistence project.

Prepared launches retain one immutable plan, source authority, target lifetime and caller intent. The persistence owner commits initial run state and admission together. Data-only ports provide current source leases, target validation and Structure delivery; this project does not depend on product contexts or components.

Acceptance, continuation, delivery and execution completion are separate results. A lost acknowledgement preserves the accepted run identity and exact exception internally; recovery reads the owner receipt and retries only outstanding continuation or delivery. Pure observation does not execute a launch. Older callers without a caller intent retain intentional-repeat behavior, without a safe-replay claim for an unacknowledged request.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Application\CanDoItAll.Processes.Application.csproj
```
