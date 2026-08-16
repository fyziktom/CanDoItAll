# Characterization-first event-session lifetime

- Run label: `SB04-INV-04-CHAR-001`
- Working directory: `repo://`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Unit.slnx --no-restore -nologo -v:minimal --filter "FullyQualifiedName~LlmChatDurableStreamEventTests.Event_session_disposal_releases_follower_lease_without_requesting_operation_cancellation"`
- Exit code: `0`

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

The test drives the production session factory and journal. Disposal leaves the operation `Running`, leaves `CancellationGeneration` at zero, and disposes one follower lease. This is the characterization-first evidence for `SB04-INV-04`.
