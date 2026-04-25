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
