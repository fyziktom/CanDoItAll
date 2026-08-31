# Combined failure regression tests

Status: implemented, built in the frozen combined candidate and **both cases passed**. The first full Integration execution independently retained both passing cases; see `combined-failure-full-integration-results.json` for exact case names, outcomes and times. No production code, live host, provider setting or live data was changed for this test subtask.

## Exact selector and expected discovery count

Project: `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`.

```text
FullyQualifiedName=CanDoItAll.Tests.Integration.AgentFramework.AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_runtime_failure_persists_failed_log_and_activity_after_store_reopen
```

Expected count: **2 xUnit theory cases**:

1. `failDuringStartup: true`: the existing test runtime observes the newly created run ID, then throws the supplied `InvalidOperationException` before its Implementation progress callback. No runtime response or provider result is constructed.
2. `failDuringStartup: false`: the existing test runtime emits its Implementation progress callback and throws `AgentRuntimeUsageException` with `AgentRuntimeFailureOrigin.Provider`, preserving an original inner `InvalidOperationException`. This is a simulated runtime/provider-origin boundary, not an HTTP adapter test.

Both cases use the same real `AgentFrameworkWorkspaceService.SendMessageAsync` failure producer, real activity coordinator and real file workspace persistence registered by the existing integration fixture. The only new fake behavior is a default-null `StartupFailure` property checked before Implementation; existing tests retain their original behavior. No test constructs a failed execution log, assistant response or tool receipt.

## Contract proven by the assertions

- The caller receives `AgentChatRunFailedException` with the actual agent, session and execution-run IDs. The original injected exception objects remain in the expected wrapper chain. Startup failures are not misclassified as provider failures; the explicit provider-origin case is classified as `ProviderError`.
- The production producer persists `ExecutionState.Failed`, `RunOutcome.Failed`, completion time and initial activity operation ID.
- Exactly one Failed log is present. A separately constructed `FileSandboxWorkspaceStore` reopens the same real backing root/scope; the complete failed log record equals the record observed before reopening. A Completed log is absent.
- The startup case has no Implementation progress; the provider-origin case retains its Implementation progress. Neither produces assistant content, output tokens, tool calls, tool receipts, artifacts or pending approvals. The original user message is deliberately retained as the only chat message.
- Replaying the real activity stream from its beginning yields exactly one terminal event: Failed with the correct run/session/agent IDs, failed outcome and `UnhandledExecutionFailure` code. No Completed activity appears.
- Synthetic secret and private-prompt sentinels are absent from the public exception messages, run result summary, persisted diagnostic log messages and activity messages. The user prompt remains in its intended chat message; the test does not incorrectly require removal from conversation storage. It does not dump exception inner graphs or private sentinels to a proof artifact.

The startup fault is after atomic run admission and before runtime progress. It does not claim to cover a pre-admission context-capture failure where no durable run exists; that has a different contract and existing unit coverage. The provider case exercises the production terminalizer/classifier/persistence/activity path with a fake runtime-origin failure. Successful real provider dispatch and genuine UI behavior remain separate MCP acceptance requirements.

## Isolation, synchronization and validation

Use the owned disposable PostgreSQL server on127.0.0.1:52049. Dot-source `.artifacts/agent-startup-performance/test-postgres/Enter-IsolatedPostgresTestEnvironment.ps1` in the same PowerShell process that launches the test command. It checks container ownership/identity/readiness and sets `CANDOITALL_TESTS_POSTGRES_CONNECTION` without printing its value. Each case uses the existing unique PostgreSQL database lease and temporary file workspace. Never use default5432, a live app database, or either live UI instance.

The fake completion barrier is released before sending, so no sleep or timing race is introduced. The caller, run-ID observation and activity reader are bounded by the existing30-second observation timeout. Activity replay starts only after the real send has terminally failed.

SB01's Integration compile/discovery finished before the test source was edited. Its already-built store/recovery tests could continue without reading the modified source. `git diff --check` passed for the test file. The combined candidate build and discovery were completed under the root gate. Both cases passed, including their execution in the first full Integration run. The source remained frozen; the original transcript and sanitized per-case proof are retained.

Production sources reviewed: `AgentFrameworkWorkspaceService.ExecutionFacade.cs`, `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, `AgentFrameworkWorkspaceExecutionService.Helpers.cs`, `AgentChatRunFailedException.cs`, `AgentExecutionActivityCoordinator.cs` and the typed activity models. Existing neighboring provider-origin, terminal-persistence and recovery tests guided the assertions; no new production seam was introduced.
