# Test budget and gates

## Why

The repository's routine solution gate is expensive. Re-running it after every class change consumes
large amounts of execution time without increasing local signal.

## Normal subbundle rules (SB00–SB10)

Allowed:

```powershell
dotnet build ./src/Modules/CanDoItAll.Modules.LlmChats/CanDoItAll.Modules.LlmChats.csproj --configuration Release
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~LlmChatDefinitionTests"
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~LlmChatPersistenceIntegrationTests"
```

Forbidden:

```powershell
dotnet test ./CanDoItAll.slnx ...
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj   # no filter
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj   # no filter
```

Use `--no-build` only when the exact tested binaries were already built in the same configuration and
dependency mode.

## Focused test families planned

- `LlmChatCanonicalModelTests`
- `LlmChatDefinitionServiceTests`
- `LlmChatConversationApplicationServiceTests`
- `EfLlmConversationStoreIntegrationTests`
- `LlmChatPersistenceIntegrationTests`
- `LlmChatsDatabaseTransferIntegrationTests`
- `LlmChatMigrationIntegrationTests`
- `LlmChatRuntimeFenceTests`
- `LlmChatOperationIdempotencyTests`
- `LlmChatOperationRecoveryTests`
- `LlmChatsApiIntegrationTests`
- `LlmChatArchitectureTests`

Names may adapt to repository conventions, but filters remain narrow.

## CP1

Run the union of focused Unit and PostgreSQL integration classes that own SB01–SB06. Do not run the
whole projects.

## CP2

Run:

- focused API real-host class;
- focused migration class;
- focused backend integration class;
- architecture and test-policy scripts.

## SB11 only

Run the repository's stable Release gate exactly once:

```powershell
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test ./CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

Use one dependency mode consistently. Also run documentation validation and the migration pending-model
check. Do not run the unfiltered suite or Playwright in this backend/API bundle.

The existing CI matrix is the Windows/Linux/macOS proof after the implementation is published.
