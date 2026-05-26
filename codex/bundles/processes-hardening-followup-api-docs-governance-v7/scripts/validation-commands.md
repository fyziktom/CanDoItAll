# Validation commands

Run from repository root.

```powershell
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py codex/bundles/processes-hardening-followup-api-docs-governance-v7 --stage prepared --repo-root .

dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessDefinitionLinterTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~ProcessStepEditorFormTests"
dotnet build CanDoItAll.slnx --no-restore

rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests codex/bundles/processes-hardening-followup-api-docs-governance-v7 -S
rg -n "processes_definition_save|processes_run_start|processes_artifact_record|AllowedOperations|OperationTargetScope|WorkflowOutputId|SubprocessChildArtifactExpectationId|BlockReasonCode|RecoveryOptions" src codex docs README* -S
```
