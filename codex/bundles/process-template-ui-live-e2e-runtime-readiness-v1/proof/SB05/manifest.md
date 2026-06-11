# SB05 Proof Manifest

## Scope
- Subbundle: `SB05`
- Invariant contract: `bundle://proof/SB05/semantic-invariants.md`
- Automation test name: `Business_plan_process_SB05_INV_001_completes_on_postgresql_through_automation_dispatch_finalizer_and_readback`
- Manual PostgreSQL contract test name: `Business_plan_process_projects_and_runs_on_postgresql`
- Process-mock impact tests: `Process_mock_catalog_seeds_role_agents_when_enabled`; `Process_mock_launch_plan_selects_expected_workflow_role_agents_when_enabled`
- Source files:
  - `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentSupport.cs`
  - `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`
  - `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
  - `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
  - `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs`
  - `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessMockAgentRuntimeIntegrationTests.cs`

## Changed-File Hashes
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentSupport.cs` SHA-256: `663F3DC5CCCD046AAC214C89C32DDF276683E8A8FAC273C338A0538E4C17D243`
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs` SHA-256: `D605D60FB10D77643547561FF6EE80FA748A39546BECCF5E4679E70CD74AD6DC`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs` SHA-256: `213F0C048DA4BC4FEDC26B3876BF2D26FD90943547D51B88780D7BD4D8E89A13`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` SHA-256: `ACF4B3B122DB629EB7A5902009247629E06D8485479029D167F6AC1CFF1A93CF`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs` SHA-256: `322D642E8054B09F2F4C1C4F7F640A042373DF312EA6D04446B93652D8301840`
- `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs` SHA-256: `A778E6CB6D0C8D2D3D953B697C0FBD0EA012B0610CC1A3428B1195DC6D082D93`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs` SHA-256: `F9F80C7219967DBB27707D478D1BFBC1B94D8EB33906F4C56C4741A07E0910DE`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessMockAgentRuntimeIntegrationTests.cs` SHA-256: `732A73C25523100B361ACE9E4F94AB72C9F23E6A16E1FFF37481716FED27AD5F`

## Source Proof
- Added business-specific process-mock roles and deterministic runtime handlers for business strategist, financial strategist, and marketing specialist in `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentSupport.cs` and `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`.
- Kept process-mock projection fallback tables aligned in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs`.
- Strengthened `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs` with an explicit PostgreSQL profile, business role mappings, outbox/execution/artifact readback assertions, managed-file checks, and non-software leakage assertions.
- Source assertion transcript: `bundle://proof/SB05/transcripts/source-assertions.txt`
- Non-software leakage scan: `bundle://proof/SB05/transcripts/business-nonsoftware-leakage-scan.txt`
- Anti-stub audit: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`

## Test Proof
- Passing PostgreSQL transcript: `bundle://proof/SB05/transcripts/postgresql-integration.txt`
- Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Business_plan_process_SB05_INV_001|FullyQualifiedName~Business_plan_process_projects_and_runs_on_postgresql"`
- Result: PostgreSQL SB05 automation and manual persistence contract tests passed with exit code 0; 2 tests passed.
- Passing process-mock impact transcript: `bundle://proof/SB05/transcripts/process-mock-catalog-impact.txt`
- Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Process_mock_catalog_seeds_role_agents_when_enabled|FullyQualifiedName~Process_mock_launch_plan_selects_expected_workflow_role_agents_when_enabled"`
- Result: catalog/launch impact tests passed with exit code 0; 2 tests passed.
- Code-first guard transcript: `bundle://proof/SB05/transcripts/code-first-guard.txt`

## Adversarial Negative Proof
- Failing-first transcript: `bundle://proof/SB05/transcripts/failing-first-source-assertion.txt`
- The failing-first source assertion exits non-zero against `HEAD` because the baseline did not contain the PostgreSQL-specific SB05 automation test name, business process-mock role mappings, persisted DB readback assertion, or business process-mock runtime/catalog roles.

## Browser Evidence
- N/A. SB05 has no browser-visible behavior.

## Semantic Adequacy
- Raw note owned: Business-analysis representative automation must be PostgreSQL-backed and non-software.
- Shipped behavior: `SB05_INV_001` proves `business-plan-development` on PostgreSQL through production launch/approval/dispatch/outbox drain, business-specific process-mock roles, exact active-step execution-run mapping, finalizer summaries, persisted run/outbox/artifact readback, and managed output files for strategy, product evidence, business plan, financial model, marketing plan, and integrated review. `SB05_INV_002` proves adding business roles did not weaken existing process-mock catalog/launch behavior.
- Shallow-pass trap: A manual transition test, a default status-only automation run, or role mappings through developer/release-manager process-mock agents could make business automation look healthy without proving PostgreSQL persistence or non-software execution.

