# Assumptions And Risks

## Working Assumptions

- The correct insertion point is `IAgentRuntime`, not `ProcessRunAutomationDispatchService`.
- The mock runtime can be selected by provider base URL and gated by `AgentFramework:ProcessMockAgents:Enabled`.
- Mock artifacts should be written through `IWorkspaceFileService` so existing execution receipts and process artifact projection remain in play.
- The QA repair path should use branch outcomes in the existing process planner rather than a bespoke loop in the mock runtime.

## Critical Path Risks

- If mock writes bypass the workspace file service, the process dispatcher may not see artifacts and the test will not prove the real path.
- If the mock catalog is seeded while disabled, normal users may see test-only agents.
- If the runtime infers behavior only from free-form prompt text, tests can become brittle; role tags and stable scenario metadata should be preferred where available.
- A full process integration test may need careful fixture setup because process definitions, assignments, artifacts, and branch outcomes are all involved.

## Validation Risks

- Direct runtime tests alone are insufficient because they do not prove process progression.
- Catalog seeding tests alone are insufficient because they do not prove deterministic QA repair behavior.
- Build-only validation is insufficient because the primary risk is orchestration semantics.

## Reopen Triggers

- Mock provider or agents appear when `AgentFramework:ProcessMockAgents:Enabled` is false.
- Any mock runtime path calls a real provider.
- QA rejection does not select a repair branch outcome.
- Repair artifacts are not visible to the process execution artifact pipeline.
- The process dispatcher requires special-case logic for mock agents.
