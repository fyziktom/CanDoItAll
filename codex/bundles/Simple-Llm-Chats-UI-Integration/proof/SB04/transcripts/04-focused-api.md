# Focused HTTP validation

- Run label: `SB04-HTTP-PASS-001`
- Working directory: `repo://`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Integration.slnx --no-build --no-restore -nologo -v:minimal --filter "FullyQualifiedName~LlmChatsConversationApiIntegrationTests"`
- Exit code: `0`

```text
Passed: 3, Failed: 0, Skipped: 0, Total: 3
```

The active response contains the exact operation GUID and preserves `hasActiveTurn: true`. The inactive bounded-page/details response omits `activeOperationId` and preserves its existing shape. Invariant IDs: `SB04-INV-01`, `SB04-INV-05`.
