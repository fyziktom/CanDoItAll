# Focused Unit validation

- Run label: `SB04-UNIT-PASS-001`
- Working directory: `repo://`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Unit.slnx --no-restore -nologo -v:minimal --filter "FullyQualifiedName~LlmChatActiveOperationProjectionTests|FullyQualifiedName~LlmChatDurableStreamEventTests.Event_session_disposal_releases_follower_lease_without_requesting_operation_cancellation|FullyQualifiedName~LlmChatWholeUseCaseProfileScopeTests.Profile_switch_after_first_read_rejects_active_operation_projection"`
- Exit code: `0`

```text
Passed: 3, Failed: 0, Skipped: 0, Total: 3
```

- Run label: `SB04-CANCEL-PASS-001`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Unit.slnx --no-build --no-restore -nologo -v:minimal --filter "FullyQualifiedName~LlmChatOperationCancellationTests"`
- Exit code: `0`

```text
Passed: 4, Failed: 0, Skipped: 0, Total: 4
```

These runs cover exact domain turn identity, unrelated inactive conversation state, terminal/compensation/abandonment clearing, profile-generation rejection without a returned value, follower ownership, and explicit cancellation. Invariant IDs: `SB04-INV-01`, `SB04-INV-02`, `SB04-INV-03`, `SB04-INV-04`.
