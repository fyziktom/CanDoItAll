# SB02: Business PostgreSQL automation reconciliation

## Objective
Resolve the mismatch between report claims and source code around PostgreSQL-backed business-analysis automation.

## Exact source references
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Implementation steps
1. Inspect `Business_plan_process_SB05_INV_001...` and determine whether it uses an explicit PostgreSQL profile.
2. If not, add a PostgreSQL-backed process-mock automation test that:
   - creates a PostgreSQL test database,
   - enables process-mock agents,
   - imports/publishes `business-plan-development`,
   - creates/approves/executes launch plan,
   - drains outbox,
   - asserts completed run/outbox/execution runs/artifacts through `AppDbContext`,
   - reads managed artifact files.
3. Keep old manual-transition PostgreSQL tests, but label them as state/persistence contract tests, not automation E2E proof.
4. Verify no software/.NET/Blazor leakage in business template or role mapping.

## Acceptance checklist
- Explicit PostgreSQL automation test exists and passes.
- Test uses process-mock launch/approval/dispatch path.
- No manual `SuppressAutomationDispatch = true` in automation proof.
- Business role mappings do not use software shortcut semantics.
