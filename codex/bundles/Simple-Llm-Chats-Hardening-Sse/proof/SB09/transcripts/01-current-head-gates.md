# Current-head gates

Repository: `C:\repositories\CanDoItAll`  
Branch: `simple-chats`  
Implementation commit: `4c71bfa8857d1228e5cb5e23fac44c9746954dfc`  
Dependency mode: local sibling source projects  
Database: PostgreSQL Testcontainers

## Expected-red semantic proof

Command:

```text
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -nologo -v:minimal -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmChatsApiPostgreSqlIntegrationTests.DurableSse_ReconnectsAfterDeltaWithoutRedispatchAndClosesAfterOneTerminalEvent"
```

The first sandbox attempt stopped before behavior on access to the configured control-plane lock. The
approved retry ran against the pre-SB09 source and failed with `KeyNotFoundException` at the assertion
for missing `replayed` admission metadata. This is the intended semantic red: the former response and
route did not provide the asynchronous replay contract or durable SSE behavior.

## Affected build

Command:

```text
dotnet build src\App\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -nologo -v:minimal -p:UseLocalCanDoItAllLibraries=true
```

Result: exit 0, 0 warnings, 0 errors. One preceding build exposed a missing namespace in the new replay
adapter; the command above is the corrected current-head result.

## Focused current-head behavior

Command:

```text
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -nologo -v:minimal -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmChatsApiPostgreSqlIntegrationTests|FullyQualifiedName~LlmChatsTurnApiIntegrationTests|FullyQualifiedName~LlmChatsIdempotencyApiIntegrationTests|FullyQualifiedName~LlmChatsCancellationApiIntegrationTests|FullyQualifiedName~LlmChatsRecoveryApiIntegrationTests|FullyQualifiedName~ApiStreamingTransportTests"
```

Result: 20 passed and 2 failed. The failures were test-only: strict string ordering of equivalent
Cache-Control directives, and a succeeded-event fixture without the model/usage evidence required by
the existing domain constructor. Production SSE behavior reached both assertions.

After correcting those assertions/fixture, only the affected pair was rerun:

```text
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -nologo -v:minimal -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmChatsApiPostgreSqlIntegrationTests.DurableSse_ReconnectsAfterDeltaWithoutRedispatchAndClosesAfterOneTerminalEvent|FullyQualifiedName~ApiStreamingTransportTests.Streaming_writer_emits_typed_public_envelopes_and_closes_at_terminal_event"
```

Result: 2 passed, 0 failed, 0 skipped in 9 seconds. The current-head focused result is therefore
compositional 22/22: 20 unchanged passes plus the corrected pair.

## Covered behavior

- prompt 202 admission while a deterministic provider remains blocked;
- explicit first/replay disposition and canonical status/events/cancel links;
- durable sequence replay through Last-Event-ID without duplicate text or redispatch;
- response disposal followed by successful operation completion;
- retained-history `stream.gap` plus authoritative status URL;
- conflicting cursor stable 400;
- explicit cancellation in status and SSE;
- provider failure redaction and terminal close;
- profile-switch closure of direct product session and HTTP projection;
- heartbeat, anti-buffering, dynamic event names, public-envelope JSON, and terminal stop;
- OpenAPI 202-only success plus events route.

## Budget record

Six filtered test attempts exceeded the normal four-command allowance by two. One was sandbox-only,
one was the required expected-red run, two stopped at compile on missing test namespaces, one executed
the 22-case union, and one reran its exact corrected pair. No unfiltered Unit/Integration project,
solution-wide suite, Playwright, LiveProcess, LongRunning, or Quarantined lane ran.
