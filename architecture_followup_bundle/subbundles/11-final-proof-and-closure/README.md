# Final proof and closure

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
