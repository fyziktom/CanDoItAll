# Final proof and closure

## Status

- Prepared

## Objective

- Close the bundle only after all gates pass, fresh proof exists for the new scope, and the live execution report matches the actual repository state.

## Covered Inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\arch_post_followup_bundle\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\reviews\02-architecture-gate-memo-log.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\codex\MASTER_TASKS.json
- C:\repositories\CanDoItAll\arch_post_followup_bundle\codex\TASKS.json
- C:\repositories\CanDoItAll\arch_post_followup_bundle\codex\VALIDATION_COMMANDS.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\traceability\01-finding-to-subbundle-map.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\traceability\01-requirement-traceability.md
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests

## Dependency Impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Audit the listed source references against the current live repository state.
2. Implement only the smallest correct change set for this subbundle.
3. Add or update the narrowest test surface that proves the stated invariant.
4. Run the required proof commands and capture fresh artifacts.
5. Update `reviews/01-execution-report.md` or the live execution report and the gate memo log before allowing downstream work to continue.

## Scope Exceptions

- Do not widen this subbundle beyond the stated objective. If the work uncovers a later-phase defect, record it and stop at the correct boundary.

## Do Not Do

- Do not continue into downstream numbered phases just because nearby files are already open.
- Do not mark this subbundle complete until the progression gate can be answered explicitly from real proof.
- If fresh proof is missing, inconsistent, or weaker than the closure claim, do not close the bundle.

## Acceptance Checklist

- Satisfy the deliverables and review questions preserved below.

## Proof Required

- Run the validation commands preserved below and record the resulting artifacts in the live execution report.

## Browser Validation Logging

- Only required if this subbundle changes visible `/processes` UI behavior beyond what component proof already covers.

## Progression Gate

- This phase is complete only when its acceptance checklist and proof artifacts are satisfied strongly enough for the next dependency to proceed without borrowed trust.

## Suggested Agent Prompt

```text
Implement only subbundle 12-final-proof-and-closure. Close the bundle only after all gates pass, fresh proof exists for the new scope, and the live execution report matches the actual repository state. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

### Purpose
Close the bundle only after all gates pass, fresh proof exists for the new scope, and the live execution report matches the actual repository state.

### Required deliverables
- Fresh build, integration, component, MCP, and migration artifacts that cover the full reopened scope.
- A final execution report that lists the real artifacts, real remaining risks, and a defensible closure decision.
- A completed architecture-gate memo log showing Gate A, Gate B, and Gate C outcomes from the live repository.
- No closure claim while any red finding from `02-open-findings.md` remains open.

### Repository touchpoints
- `reviews/01-execution-report.md`
- `reviews/02-architecture-gate-memo-log.md`
- `codex/MASTER_TASKS.json`
- `codex/TASKS.json`
- `codex/VALIDATION_COMMANDS.md`
- `traceability/01-finding-to-subbundle-map.md`
- `traceability/01-requirement-traceability.md`
- `tests/CanDoItAll.Tests.Integration`
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Mcp.Processes.Tests`

### Validation commands
- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessSchemaIntegrationTests|FullyQualifiedName~ProcessOutboxIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj -v:minimal`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --startup-project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --context AppDbContext`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext`

### Review questions
1. Do the fresh proof artifacts actually cover every reopened scope item in this follow-up?
2. Do the execution report and gate memos now agree with the live repository state?
3. Is any remaining risk low enough to document honestly without blocking closure?

### Corrective trigger
If fresh proof is missing, inconsistent, or weaker than the closure claim, do not close the bundle.

### Corrective template
- `subbundles/_corrective-proof-reset`

### Detailed execution notes
- The closure bar here is higher than in the prior round because the previous follow-up over-claimed closure.
- Be explicit if closure is still not justified. A clean reopen is better than another premature sign-off.

