# SB08 Proof Manifest

## Status
- Result: Blocked
- Completed: 2026-06-11
- Scope: Release matrix, final red-team scans, live OpenAI opt-in classification, code-first ratio gate, and release decision.

## Source Hashes
- `tests\CanDoItAll.Tests.Unit\LocalWorkspaceProcessHostTests.cs`
  - SHA256: `32FE4E52FF82ECEDB2FA53B5F336DB37EF4209E84C88E11DF54D57EBD42DC6E0`
- `tests\CanDoItAll.Tests.Integration\ProcessTemplateExecutionE2ETests.cs`
  - SHA256: `1B015FC87D1E252D8BE309E484FC60DE19AC354FD1D7DF06CD05576D3436B722`
- `tests\CanDoItAll.Tests.Integration\BusinessPlanProcessPostgresIntegrationTests.cs`
  - SHA256: `A778E6CB6D0C8D2D3D953B697C0FBD0EA012B0610CC1A3428B1195DC6D082D93`
- `tests\CanDoItAll.Tests.Integration\ProcessDomainEvidenceReadOnlyAdapterTests.cs`
  - SHA256: `81619C12E31895609224B812E55B0D8FB6CBC4990D32112C3B919D7CAE41706F`
- `tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
  - SHA256: `F593AE88B2E4B9130063D9808C8BE725C89352B1566E3FD8643547B8AFD5EEA3`
- `tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectScopedProcessLaunch.cs`
  - SHA256: `15A9B0AA071373BBB1871F9E6B1D1338D11183CE838D9235496E13B21E6C6126`
- `src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
  - SHA256: `D605D60FB10D77643547561FF6EE80FA748A39546BECCF5E4679E70CD74AD6DC`
- `src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentSupport.cs`
  - SHA256: `663F3DC5CCCD046AAC214C89C32DDF276683E8A8FAC273C338A0538E4C17D243`

## Transcripts
- Passing release matrix: `bundle://proof/SB08/transcripts/build.txt`
- Passing unit matrix: `bundle://proof/SB08/transcripts/unit-tests.txt`
- Passing focused integration matrix: `bundle://proof/SB08/transcripts/focused-integration-matrix.txt`
- Passing Playwright proof: `bundle://proof/SB08/transcripts/playwright-sb02.txt`
- Live OpenAI classification: `bundle://proof/SB08/transcripts/live-openai-classification.txt`
- Source scan: `bundle://proof/SB08/transcripts/source-core-drift-scan.txt`
- Source scan: `bundle://proof/SB08/transcripts/driver-registration-reflection-fallback-scan.txt`
- Source scan: `bundle://proof/SB08/transcripts/mutation-api-readonly-scan.txt`
- Source scan: `bundle://proof/SB08/transcripts/secret-leakage-scan.txt`
- Source scan: `bundle://proof/SB08/transcripts/bundle-path-coupling-scan.txt`
- Source scan: `bundle://proof/SB08/transcripts/large-file-growth-scan.txt`
- Anti-stub audit: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`
- Final fake-proof audit: `bundle://proof/SB08/transcripts/fake-proof-audit.txt`
- Completed-stage validator: `bundle://proof/SB08/transcripts/completed-stage-validator.txt`
- Blocking ratio proof: `bundle://proof/SB08/transcripts/final-code-first-ratio.txt`

## Validation Commands
- `dotnet build CanDoItAll.slnx --configuration Debug --no-restore`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Debug --no-restore`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Blazor_app_delivery_template_SB03_INV|FullyQualifiedName~Software_delivery_template_SB04_INV_001|FullyQualifiedName~Process_template_catalog_SB04_INV_002|FullyQualifiedName~Business_plan_process_SB05_INV_001|FullyQualifiedName~Business_plan_process_projects_and_runs_on_postgresql|FullyQualifiedName~Process_mock_catalog_seeds_role_agents_when_enabled|FullyQualifiedName~Process_mock_launch_plan_selects_expected_workflow_role_agents_when_enabled|FullyQualifiedName~Process_runtime_host_readback_SB06_INV_001|FullyQualifiedName~StartRunFromTriggerAsync_SB07_INV_001|FullyQualifiedName~Process_readonly_verification_job_runner_SB07_INV_001"`
- `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Project_structure_process_template_launch_SB02_INV_001_launches_approved_template_from_structure_context_and_reads_back_run"`

## Browser Validation
- SB02 large-desktop proof passed again in SB08: `bundle://proof/SB08/transcripts/playwright-sb02.txt`.
- Screenshot set remains under `bundle://proof/SB02/screenshots/`.
- Route scope: `route:projects-{projectId}-processes`, `route:projects-{projectId}-structure`, and `route:projects-{projectId}-processes-query-processId-{definitionId}-runId-{runId}`.
- Viewport: 1900x1200 large desktop.

## Release Decision
- Decision document: `bundle://proof/SB08/release-decision.md`.
- Zip artifact: `bundle://artifacts/process-template-ui-live-e2e-runtime-readiness-v1.zip`.
- Processes are restored for the tested user-facing launch, representative automation, PostgreSQL business-analysis, runtime-host readback, and scheduler/workflow trigger paths.
- Final bundle closure remains blocked by the code-first ratio gate under the conservative `HEAD` baseline.

## Semantic Contract
- `bundle://proof/SB08/semantic-invariants.md`



