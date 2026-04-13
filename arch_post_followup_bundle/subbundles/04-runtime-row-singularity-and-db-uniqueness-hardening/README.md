# Runtime row singularity and DB uniqueness hardening

## Status

- Prepared

## Objective

- Bring the runtime schema in line with service assumptions by enforcing singular step runs and singular run assignments directly in the database, including null-safe uniqueness for step-scoped versus run-scoped assignments.

## Covered inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact source references

- src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs
- src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs
- src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs
- src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.Operations.cs
- src/CanDoItAll.Migrations.Sqlite/Migrations
- src/CanDoItAll.Migrations.PostgreSql/Migrations
- tests/CanDoItAll.Tests.Integration/ProcessSchemaIntegrationTests.cs
- tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Dependency impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation depth

- `Critical foundation`

## Implementation steps

1. Audit the listed source references against the current live repository state.
2. Implement only the smallest correct change set for this subbundle.
3. Add or update the narrowest test surface that proves the stated invariant.
4. Run the required proof commands and capture fresh artifacts.
5. Update `reviews/01-execution-report.md` or the live execution report and the gate memo log before allowing downstream work to continue.

## Scope exceptions

- Do not widen this subbundle beyond the stated objective. If the work uncovers a later-phase defect, record it and stop at the correct boundary.

## Do not do

- Do not continue into downstream numbered phases just because nearby files are already open.
- Do not mark this subbundle complete until the progression gate can be answered explicitly from real proof.
- If runtime singularity still depends only on service discipline or nullable non-unique indexes, stop and open the runtime-uniqueness corrective playbook.

## Acceptance checklist

- Satisfy the deliverables and review questions preserved below.

## Proof required

- Run the validation commands preserved below and record the resulting artifacts in the live execution report.

## Browser validation logging

- Only required if this subbundle changes visible `/processes` UI behavior beyond what component proof already covers.

## Progression gate

- This phase is complete only when its acceptance checklist and proof artifacts are satisfied strongly enough for the next dependency to proceed without borrowed trust.

## Suggested agent prompt

```text
Implement only subbundle 04-runtime-row-singularity-and-db-uniqueness-hardening. Bring the runtime schema in line with service assumptions by enforcing singular step runs and singular run assignments directly in the database, including null-safe uniqueness for step-scoped versus run-scoped assignments. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved bundle notes

### Purpose
Bring the runtime schema in line with service assumptions by enforcing singular step runs and singular run assignments directly in the database, including null-safe uniqueness for step-scoped versus run-scoped assignments.

### Required deliverables
- A unique runtime invariant for one `ProcessStepRun` per `(ProcessRunId, StepDefinitionId)`.
- A unique runtime invariant for one `ProcessRunAssignment` per `(ProcessRunId, RoleRequirementId)` at run scope and per `(ProcessRunId, RoleRequirementId, StepDefinitionId)` at step scope.
- Friendly service-boundary handling for uniqueness conflicts where concurrency races can still occur.
- Fresh integration tests proving duplicate runtime rows are rejected and concurrent assignment resolution cannot create duplicate rows silently.

### Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.Operations.cs`
- `src/CanDoItAll.Migrations.Sqlite/Migrations`
- `src/CanDoItAll.Migrations.PostgreSql/Migrations`
- `tests/CanDoItAll.Tests.Integration/ProcessSchemaIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

### Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessSchemaIntegrationTests|FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --startup-project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --context AppDbContext`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext`

### Review questions
1. Can the DB still accept two step runs for the same run and step definition?
2. Can the DB still accept duplicate run assignments for the same logical scope when `StepDefinitionId` is null or non-null?
3. Do the runtime services still contain any `FirstOrDefault` or dictionary assumptions that are stronger than the schema guarantees?

### Corrective trigger
If runtime singularity still depends only on service discipline or nullable non-unique indexes, stop and open the runtime-uniqueness corrective playbook.

### Corrective template
- `subbundles/_corrective-runtime-uniqueness-reset`

### Detailed execution notes
- This is not merely defensive schema polish. `ResolveAssignmentAsync` currently has a real concurrent duplicate-insert race without a unique index.
- Do not rely on one giant nullable composite index if the null semantics differ between providers.
