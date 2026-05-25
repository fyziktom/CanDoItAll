# Validation Commands

```powershell
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py codex/bundles/processes-hardening-followup-runtime-correctness-v6 --stage prepared --repo-root .

dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~AgentWorkspaceToolAccessMetadataTests"

dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessDefinitionLinterTests"

dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~Process"

dotnet build CanDoItAll.slnx --no-restore

rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests codex/bundles/processes-hardening-followup-runtime-correctness-v6 -S

python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py codex/bundles/processes-hardening-followup-runtime-correctness-v6 --stage completed --repo-root .
```
