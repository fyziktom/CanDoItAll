# Current-head gates

All commands ran from `C:\repositories\CanDoItAll` in local sibling dependency mode.

Implementation commit: `4212914dd52415c00d12e9d33b35aaad34260531`.

## Final focused compatibility union

Invariant coverage: `SBI-07-01`, `SBI-07-02`, `SBI-07-03`, `SBI-07-04`.

```powershell
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -nologo -v:minimal -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~ProviderBackedLlmInvocationAdapterTests|FullyQualifiedName~ProviderBackedLlmStreamingInvocationAdapterTests|FullyQualifiedName~ProviderStreamingDriverTests|FullyQualifiedName~ConcreteProviderDriverTests|FullyQualifiedName~LlmChatInvocationAuditTests"
```

- exit: 0
- passed: 86
- failed: 0
- skipped: 0
- duration reported by test host: 186 ms

The union covers the unchanged completed port, incremental/fallback adapter, fragmented OpenAI and
Azure SSE, fragmented Ollama NDJSON, Responses hidden-reasoning filtering, malformed-frame
redaction, retry/empty/deadline/cancellation behavior, and durable per-attempt ordinal/usage audit.

## Final affected build

```powershell
dotnet build src/Modules/CanDoItAll.Modules.LlmChats.Persistence/CanDoItAll.Modules.LlmChats.Persistence.csproj --no-restore -nologo -v:minimal -p:UseLocalCanDoItAllLibraries=true
```

- exit: 0
- warnings: 0
- errors: 0

No EF model or migration changed, so no database/model command was required for SB07.
