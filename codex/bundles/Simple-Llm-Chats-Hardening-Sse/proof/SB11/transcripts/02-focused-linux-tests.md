# Focused Linux tests

Every command used package mode, the same container-only NuGet config, PostgreSQL 16 through
`host.docker.internal`, and `CANDOITALL_TEST_REPOSITORY_ROOT=/src` where repository source assertions
needed an explicit identity.

| Command | Exit | Result |
|---|---:|---|
| focused Unit union for `ProviderStreamingDriverTests`, concrete/provider adapters, `LlmChatOperation*`, durable events, and LLM Chat boundaries/composition | 1 | 105 passed, 1 failed, 0 skipped. The only failure was the isolated-artifact repository-root locator; all provider/parser/retry/state/event cases passed. |
| exact `ConcreteDrivers_ConsumersUseProviderRuntimeAdoptionBoundaries` after explicit-root repair | 0 | 1 passed, 0 failed, 0 skipped in 54 ms. |
| `dotnet test ...CanDoItAll.Tests.Integration.csproj ... --filter "FullyQualifiedName~LlmChat|FullyQualifiedName~ApiStreamingTransportTests"` | 0 | 43 passed, 0 failed, 0 skipped in 1:17. |

The third command consolidates the prescribed PostgreSQL/backend and HTTP/SSE selections into one
focused Integration union so the fixture repair could be proven without exceeding CP2's three-command
ceiling. No solution-wide, unfiltered, Playwright, LiveProcess, LongRunning, or Quarantined lane ran.
