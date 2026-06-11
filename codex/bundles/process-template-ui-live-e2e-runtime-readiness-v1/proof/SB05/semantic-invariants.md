# SB05 Semantic Invariants

## Invariant SB05_INV_001
- Invariant ID: `SB05_INV_001`
- Source raw note: Prove the business-analysis representative process on PostgreSQL without borrowing software/.NET/Blazor roles or shortcuts.
- Expected behavior: The `business-plan-development` template runs on a PostgreSQL test profile through process-mock launch/approval/dispatch, completes the approved business-plan path, records completed outbox records, maps one process-mock execution run to each active-path automated step, uses business strategist, financial strategist, and marketing specialist process-mock roles, persists run/outbox/artifact readback through `AppDbContext`, and reads back managed files for strategy, product evidence, business plan, financial model, marketing plan, and integrated review artifacts.
- Disallowed shallow implementation: Reusing developer/release-manager process-mock roles for finance or marketing, proving only manual transitions, suppressing automation dispatch, checking only step statuses, accepting artifact records without managed files, or relying on an in-memory/non-PostgreSQL test profile.
- Failing-first test: `bundle://proof/SB05/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB05/transcripts/postgresql-integration.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentSupport.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`
- Production assertions: `AssertBusinessAutomationDispatchReadback` verifies completed outbox records, process-mock source/provider/model, run id, exact active-step execution-run mapping, completed execution state, succeeded outcome, and the three business process-mock role keys. `AssertPersistedBusinessAutomationReadbackAsync` verifies the completed `ProcessRun`, completed persisted outbox records, persisted artifact records, and required business artifact titles through PostgreSQL-backed `AppDbContext`.
- Red-team negative case: `bundle://proof/SB05/transcripts/business-nonsoftware-leakage-scan.txt` parses business template step/artifact fields and proves no software/.NET/Blazor/developer/implementation/release-manager/QA leakage, while source assertions prove SB05 no longer maps business roles to `ProductOwner`, `Developer`, or `ReleaseManager` process-mock shortcuts.

## Invariant SB05_INV_002
- Invariant ID: `SB05_INV_002`
- Source raw note: Extending process-mock for business scenarios must not break existing delivery process-mock fixtures.
- Expected behavior: The process-mock catalog seeds the new business-role agents while existing workflow fixtures assert only the role keys they actually declare.
- Disallowed shallow implementation: Treating every process-mock catalog role as required in every fixture, or weakening catalog seeding so business roles are not available for launch selection.
- Passing test: `bundle://proof/SB05/transcripts/process-mock-catalog-impact.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessMockAgentRuntimeIntegrationTests.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentSupport.cs`
- Production assertions: `Process_mock_catalog_seeds_role_agents_when_enabled` still verifies every catalog role is seeded and projected. `Process_mock_launch_plan_selects_expected_workflow_role_agents_when_enabled` verifies the legacy workflow fixture binds its six delivery roles without assuming business roles are part of that workflow.
- Downstream dependency check: SB06 can use the PostgreSQL-backed business automation run as a real run/readback source without depending on software-role process-mock agents.
