# SB10 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs:55` resolves the effective lint mode from the publish request and persisted definition risk profile before publication validation, and `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs:56` runs the linter with that effective mode.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs:455` resolves the effective lint mode from the run-start request and persisted definition risk profile, and `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs:456` runs the linter with that effective mode before a run can start.
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs:322` defines `ResolveEffectiveLintMode`, and `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs:334` requires strict lint for high or mission-critical definitions and guarded or delegated autonomy.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionForm.razor:177` renders every lint issue from `Model.LintResult.Issues`; the previous four-item cap is gone.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs:1509` covers high-criticality publish blocking through the production service path, and `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs:1532` covers delegated-autonomy run-start blocking through the production service path.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs:3614` builds the product-mutation fixture without a typed operation contract used by the publish/start regression tests.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs:230` proves a non-mutating architecture report is not forced into a product-mutation contract by strict lint.
- `repo://tests/CanDoItAll.Tests.Components/ProcessDefinitionFormTests.cs:10` proves the definition form renders all lint issues, including the fifth issue that the old cap hid.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Effective lint mode | `ResolveEffectiveLintMode` combines caller intent with persisted definition `Criticality` and `AutonomyLevel` | Publish and run-start linter gates | Recomputed on each publish/start attempt; no hidden fallback state is stored | Failing-first service tests showed high-criticality publish and delegated run-start previously succeeded under advisory lint, then passing tests prove they block |
| Full lint issue list in the editor | `ProcessDefinitionLinter.Analyze` populates `ProcessDefinitionEditorModel.LintResult` | `ProcessDefinitionForm` lint panel | Current editor state is rendered directly; no truncation or derived shadow list | Failing-first component test proved the fifth issue was hidden before removing the cap, then passing test proves all five issue codes render |
| Generic red-team lint scope | `ProcessDefinitionLinter.Analyze` boundary rules distinguish product mutation from report/review/business/legal/manufacturing/research work | Strict publish/start gates and lint dry-runs | Evaluated per definition model without Blazor/.NET-only hardcoding unless the fixture intentionally describes product mutation | Red-team linter tests prove non-product-mutation scenarios remain valid while product-mutation steps without typed contracts are blocked |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB10/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB10/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB10/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB10/transcripts/changed-file-hashes.txt`

## Validation

Passed:

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SB10_INV_001" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~Render_SB10_INV_001" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessDefinitionLinterTests|FullyQualifiedName~PublishAsync_SB10_INV_001|FullyQualifiedName~StartRunAsync_SB10_INV_001" --no-restore --no-build -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessDefinitionFormTests|FullyQualifiedName~ProcessStepEditorFormTests" --no-restore --no-build -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Process_step_operation_contract_editor_controls_work_in_browser" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~AgentWorkspaceToolAccessMetadataTests" -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -v minimal`
- `dotnet build CanDoItAll.slnx --no-restore -m:1 -v minimal`
- `rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests codex/bundles/processes-hardening-followup-runtime-integrity-v4 -S`
- `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py --stage prepared codex/bundles/processes-hardening-followup-runtime-integrity-v4`

Attempted but did not complete:

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -v minimal` timed out after 15 minutes.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --no-build --logger "trx;LogFileName=full-integration-sb10.trx" -v minimal` timed out after 30 minutes and did not produce a TRX result.

Known unrelated warning noise: MSB3277 reports existing EntityFrameworkCore.Relational 10.0.0/10.0.4 conflicts during build/test.

## Blockers

The full integration suite did not complete within the available command windows. Focused SB10 coverage, the required targeted integration slice, full unit tests, build, browser editor smoke, source audits, and bundle validation passed.
