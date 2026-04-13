# Final proof and closure

## Status

- Completed

## Objective

- Run the final proof matrix, reconcile the execution report with the actual artifacts, and close only if no red finding remains.

## Covered Inputs

- See `C:\repositories\CanDoItAll\architecture_followup_bundle\02-open-findings.md`, `C:\repositories\CanDoItAll\architecture_followup_bundle\requirements\01-normalized-requirements.md`, and `C:\repositories\CanDoItAll\architecture_followup_bundle\traceability\01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `C:\repositories\CanDoItAll\architecture_followup_bundle\codex\TASKS.json` and `C:\repositories\CanDoItAll\architecture_followup_bundle\plan\01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\architecture_followup_bundle\05-proof-contract.md
- C:\repositories\CanDoItAll\architecture_followup_bundle\reviews\00-execution-report-template.md
- C:\repositories\CanDoItAll\architecture_followup_bundle\reviews\01-architecture-gate-memo-log-template.md
- C:\repositories\CanDoItAll\.codex-test-results
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
3. Run the required proof commands and capture the resulting artifacts while the state is fresh.
4. Update `C:\repositories\CanDoItAll\architecture_followup_bundle\reviews\01-execution-report.md` and any gate log or follow-up artifact before allowing downstream work to continue.

## Scope Exceptions

- Do not widen this subbundle beyond the stated objective. If the work uncovers a later-phase defect, record it and stop at the correct boundary.

## Do Not Do

- Do not widen scope into later numbered phases just because the same files are nearby.
- If any red finding is still open, or if the proof artifacts are weaker than the prose claim, fail final closure and create a corrective subbundle from the generic template.
- Do not mark the subbundle complete until the progression gate can be answered explicitly from real proof.

## Acceptance Checklist

- Satisfy the deliverables and review questions preserved below.

## Proof Required

- Run the validation commands preserved below and record the resulting artifacts in the live execution report.

## Browser Validation Logging

- Capture fresh `/processes` Playwright proof with screenshots if the workspace or canvas structure changes during this phase.

## Progression Gate

- This phase is complete only when its acceptance checklist and proof artifacts are satisfied strongly enough for the next dependency to proceed without borrowing trust.

## Suggested Agent Prompt

```text
Implement only subbundle 11-final-proof-and-closure. Run the final proof matrix, reconcile the execution report with the actual artifacts, and close only if no red finding remains. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

## Purpose

Run the final proof matrix, reconcile the execution report with the actual artifacts, and close only if no red finding remains.

## Required deliverables

- Fresh build, integration, component, migration, and any required browser proof artifacts.
- A final execution report that exactly matches the produced proof artifacts.
- A completed architecture gate memo log.
- An explicit closure statement that no red finding from `02-open-findings.md` remains.

## Repository touchpoints

- `05-proof-contract.md`
- `reviews/00-execution-report-template.md`
- `reviews/01-architecture-gate-memo-log-template.md`
- `.codex-test-results`
- `tests/CanDoItAll.Tests.Integration`
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Mcp.Processes.Tests`

## Validation commands

- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj -v:minimal`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --startup-project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --context AppDbContext`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext`

## Review questions

1. Do the proof artifacts now match the closure claim exactly?
2. Is every red finding from `02-open-findings.md` explicitly closed?
3. Would a fresh reviewer be able to confirm closure from code and artifacts alone?

## Corrective trigger

If any red finding is still open, or if the proof artifacts are weaker than the prose claim, fail final closure and create a corrective subbundle from the generic template.

## Corrective template

- `subbundles/_corrective-template`

## Final closure rule

Final closure is allowed only when:
- the code is hardened;
- the schema enforces the claimed invariants;
- the side-effect boundary is durable enough;
- the proof artifacts and the final report agree.
