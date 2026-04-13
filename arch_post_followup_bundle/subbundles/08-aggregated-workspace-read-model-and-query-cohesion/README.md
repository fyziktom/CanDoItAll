# Aggregated workspace read model and query cohesion

## Status

- Prepared

## Objective

- Reduce workspace chattiness and torn-read risk by introducing a more cohesive read boundary for workspace and run details instead of many sequential service calls with separate `DbContext` instances.

## Covered inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact source references

- src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs
- src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs
- src/CanDoItAll.Modules.Processes/ProcessesService.cs
- src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs
- src/CanDoItAll.Modules.Processes/ProcessesService.RuntimeReadQuery.cs
- tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs
- tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Dependency impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation depth

- `High-value follow-up`

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
- If query cohesion work widens into a risky rewrite or leaves the same torn-read pattern intact, stop and open the query-cohesion corrective playbook.

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
Implement only subbundle 08-aggregated-workspace-read-model-and-query-cohesion. Reduce workspace chattiness and torn-read risk by introducing a more cohesive read boundary for workspace and run details instead of many sequential service calls with separate `DbContext` instances. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved bundle notes

### Purpose
Reduce workspace chattiness and torn-read risk by introducing a more cohesive read boundary for workspace and run details instead of many sequential service calls with separate `DbContext` instances.

### Required deliverables
- A cohesive read model or query service for run details at minimum, and preferably for the broader workspace payload if the scope stays controlled.
- A documented consistency boundary so one user-visible workspace refresh is not stitched from unrelated `DbContext` snapshots without intention.
- Selective `AsNoTracking` where full entities are materialized for read-only work; do not add cargo-cult `AsNoTracking` to pure DTO projections that already avoid tracking.
- Component/integration tests showing the workspace still renders correctly after the query-cohesion refactor.

### Repository touchpoints
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.RuntimeReadQuery.cs`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

### Validation commands
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`

### Review questions
1. Is run-details loading still a bundle of many sequential service calls with separate contexts?
2. Does the refactor create a clear consistency boundary for a workspace refresh?
3. Were unnecessary tracking loads reduced only where they were real, without noisy no-op changes?

### Corrective trigger
If query cohesion work widens into a risky rewrite or leaves the same torn-read pattern intact, stop and open the query-cohesion corrective playbook.

### Corrective template
- `subbundles/_corrective-query-cohesion-reset`

### Detailed execution notes
- This phase should improve architecture and latency, not just move code around.
- Keep the existing query seams that Codex already extracted; extend them instead of collapsing them back into `ProcessesService`.
