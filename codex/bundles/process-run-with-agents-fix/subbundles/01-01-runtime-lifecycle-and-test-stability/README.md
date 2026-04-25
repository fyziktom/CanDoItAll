# 01 runtime lifecycle and test stability

## Status

- `Completed`

## Objective

Stabilize the process runtime and validation harness so focused process, outbox, dispatcher, and template tests can run reliably before any E2E mock-agent process is attempted.

## Covered Inputs

- REQ-001: prevent untracked process automation background work from holding SQLite/scoped resources after tests or hosts dispose.
- REQ-002: restore process-template validation test executability.
- REQ-003: align stale dispatcher tests with current production signatures and strict completion behavior.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessOutbox.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\LocalRuntimeHostedWorkerPolicy.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessOutboxIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplatePackLoaderTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\TestApplication.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\TestApplicationBootstrap.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\CanDoItAllTestEnvironment.cs`

## Deliverables

- Deterministic lifecycle for automation kickoff after `StartRunAsync`.
- Test-controllable outbox processing path that does not rely on unobserved `Task.Run`.
- Fixed process-template test DI setup for the `AddProcessesModule` signature.
- Updated dispatch tests that call current method signatures and assert current intended completion semantics.
- Focused tests that pass without teardown file-lock failures.

## Dependency Impact

- This is a critical foundation.
- Subbundles 02 through 05 must not start implementation proof until this gate passes, because lingering background dispatch can corrupt E2E results and make SQLite failures look like process logic failures.

## Validation Depth

- Critical foundation.
- Backend runtime and test-suite closure.

## Implementation Steps

1. Reproduce the current teardown lock with the focused `ProcessOutboxIntegrationTests` and branch/dependency process-service tests.
2. Inspect `TriggerOutboxProcessingInBackground` and decide the smallest testable lifecycle change, such as an injectable kickoff service, awaitable background task tracker, or configuration-controlled eager kickoff path.
3. Ensure durable outbox processing remains available in production lanes and that tests can explicitly drain process automation dispatch.
4. Repair `ProcessTemplatePackLoaderTests` and any helper setup to provide `IConfiguration` when calling `AddProcessesModule`.
5. Update stale reflection calls in `ProcessRunAutomationDispatchServiceTests` to match current method signatures.
6. Re-evaluate tests that expected `Blocked` or `Completed` where production now returns `Failed`; update test expectations only when the current behavior is intentional and documented by the dispatcher contract.
7. Run focused validation commands and repeat any previously file-locking tests individually.

## Scope Exceptions

- Unrelated full-solution compile failures outside process/template/runtime tests are not owned here unless they block focused process validation.
- No mock-agent E2E process is implemented in this subbundle.

## Do Not Do

- Do not remove durable outbox processing.
- Do not hide dispatch failures by swallowing exceptions without durable state.
- Do not weaken governed completion rules to make old tests pass.
- Do not introduce broad sleeps as the primary file-lock fix.

## Acceptance Checklist

- `ProcessOutboxIntegrationTests` focused run passes without `primary.db` teardown locks.
- The two individually failing `ProcessesServiceIntegrationTests` branch/dependency tests pass without `primary.db` teardown locks.
- Focused process-template tests compile and run.
- Dispatch tests no longer fail from reflection parameter-count mismatches.
- Any changed completion expectation is documented in test names or assertions.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessOutboxIntegrationTests"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"`
- `dotnet test tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-restore --filter "FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~CurrentArchitectureTemplateParityTests"`

## Browser Validation Logging

- N/A. Backend runtime/test stability only.

## Progression Gate

- Downstream subbundles may proceed only after the focused test runs above are executable and the file-lock failures are gone.

## Suggested Agent Prompt

```text
Implement subbundle 01 only from C:\repositories\CanDoItAll\codex\bundles\process-run-with-agents-fix. Stabilize process runtime lifecycle and test stability without changing mock-agent behavior or weakening dispatcher governance. Update the execution report with exact test results.
```
