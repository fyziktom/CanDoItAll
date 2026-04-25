# Analysis Test Results

## Summary

These commands were run during bundle preparation to identify current weak spots. They are not final proof for implementation closure.

## Commands And Outcomes

| Command | Outcome | Findings |
| --- | --- | --- |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests"` | Passed | 3/3 tests passed. Mock provider gating, catalog seeding, QA rejection, repair artifact, and QA approval work when mock agents are invoked directly through AgentFramework workspace execution. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessOutboxIntegrationTests"` | Failed | 113/130 tests passed. The 17 failures are all in `ProcessRunAutomationDispatchServiceTests`, mostly stale reflection invocations and expected completion outcomes that no longer match current strict behavior. |
| `dotnet test tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-restore --filter "FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~CurrentArchitectureTemplateParityTests"` | Failed to compile | `ProcessTemplatePackLoaderTests.cs` still calls `AddProcessesModule` without the required `IConfiguration` argument. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "<focused ProcessesServiceIntegrationTests subset>"` | Failed at teardown | 5/7 tests passed. Two branch/dependency tests reached teardown then failed because `primary.db` was still locked. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessOutboxIntegrationTests"` | Failed at teardown | 6/7 tests passed. `StartRunAsync_remains_successful_when_activity_dispatch_requires_retry` failed because `primary.db` was still locked during cleanup. |

## Interpreted Weak Spots

- Mock-agent runtime is usable directly.
- Process runtime lifecycle is not deterministic enough for reliable E2E tests.
- Template validation is blocked by test compile drift.
- Dispatcher tests are no longer aligned with production method signatures and stricter completion rules.
- No existing test proves `StartRunAsync` through durable process automation dispatch to final run completion with mock agents.

## Implementation Proof Results

| Subbundle | Command | Outcome | Notes |
| --- | --- | --- | --- |
| 01 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessOutboxIntegrationTests"` | Passed | 7/7 tests passed; no `primary.db` teardown lock after removing unobserved eager automation dispatch. |
| 01 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path"` | Passed | 1/1 branch-routing test passed. |
| 01 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready"` | Passed | 1/1 dependency-join test passed. |
| 01 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"` | Passed | 120/120 dispatcher tests passed after aligning fixtures with current concrete proof semantics. |
| 01 | `dotnet test tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-restore --filter "FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~CurrentArchitectureTemplateParityTests"` | Passed | 16/16 template/catalog/projection/parity tests passed. |
| 02 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Calculator_repair_process"` | Passed | 1/1 calculator process graph test passed. It proves mock-compatible role keys, `repairs-required`/`approved` branch keys, runtime branch selection IDs, skipped non-selected direct-release path, required artifact enforcement, QA repair path, QA recheck approval, final release notes, and completed run status without AgentFramework. |
| 03 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests"` | Passed | 4/4 mock provider/catalog/runtime/staffing tests passed. New launch staffing proof asserts each calculator role selects the expected process mock party, has a bound technical agent ID matching CRM-HR staffing facts, and uses the mock provider/model. |
| 03 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessLaunchPlanningIntegrationTests"` | Passed | 5/5 existing launch-planning regression tests passed after adding exact AI tag alias scoring. |
| 04 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"` | Passed | 125/125 dispatcher tests passed. New coverage proves process mock artifact projection, QA rejection branch selection, QA approval branch selection, mismatched required artifact blocking, and missing technical-agent binding diagnostics while preserving strict non-mock governed completion tests. |
| 04 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests"` | Passed | 4/4 mock runtime/staffing tests still pass against the dispatcher contract changes. |
| 05 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Process_mock_calculator_process_completes_end_to_end_through_durable_outbox_dispatch"` | Passed | 1/1 new E2E test passed. The process starts from process service APIs, drains durable outbox dispatch, uses mock agents only, completes QA rejection, repair, QA approval, release notes, linked process artifacts, and completed outbox records without dead letters. |
| 05 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests"` | Passed | 5/5 mock runtime tests passed, including the new E2E regression. |
| 05 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessOutboxIntegrationTests"` | Passed | 7/7 process outbox tests passed after E2E changes. |
| 05 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"` | Passed | 125/125 dispatcher tests passed after E2E changes. |
