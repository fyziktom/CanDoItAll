# Validation Commands

Run from repository root.

```powershell
# Prepared bundle validation
pwsh ./codex/skills/bundles/candoitall-bundle-workflow/scripts/validate_bundle.ps1 `
  -BundlePath ./codex/bundles/processes-hardening-followup-runtime-governance-v5

# Focused integration tests
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore `
  --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests"

# Focused unit tests
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore `
  --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~AgentWorkspaceToolAccessMetadataTests"

# Broader tests
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore

# Build
dotnet build CanDoItAll.slnx --no-restore

# PostgreSQL-only audit
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests codex/bundles/processes-hardening-followup-runtime-governance-v5 -S
```

If the repository uses a different prepared-bundle validator path, use the canonical validator from `codex/skills/bundles/candoitall-bundle-workflow/SKILL.md`.
