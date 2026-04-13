# Proof contract

A subbundle is not done until its proof exists in artifacts and the artifacts match the claimed scope.

## Mandatory proof categories

### Build / compile
- `dotnet build CanDoItAll.slnx -v:minimal`

### Integration proof
At minimum, fresh `.trx` files must exist for:

- Process service integration coverage
- schema/invariant integration coverage
- component coverage for workspace/canvas after structural work
- MCP process coverage if affected

### Migration proof
Generate migration scripts for both providers after schema work:

- SQLite migrations script
- PostgreSQL migrations script

### Browser proof
Browser proof is required again if the workspace/UI structure changes in subbundle `10`.

## Minimum command matrix

### Process integration
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests" -v:minimal`

### Component tests
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`

### MCP process tests
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj -v:minimal`

### Provider migration scripts
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --startup-project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --context AppDbContext`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext`

## Additional proof required by this follow-up

- a direct integration proof that duplicate unconditional dependencies are rejected by the DB;
- a direct integration proof that orphan runtime/definition child rows are rejected by the DB;
- a direct integration proof that single-draft / single-published invariants are enforced;
- proof that activity/search side effects are durable or retriable after a forced dispatch failure.

## Required report updates

For every gate and final closure, update:

- `reviews/00-execution-report-template.md` or its live counterpart
- `reviews/01-architecture-gate-memo-log-template.md` or its live counterpart

Do not mark a gate or final closure as passed until those documents and the proof artifacts agree.
