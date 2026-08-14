# Focused command catalog

Commands are templates. The executor must preserve the current dependency mode and adjust exact test
class names only when SB00 records the real names.

## Project builds

```powershell
dotnet build ./src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/CanDoItAll.AgentFramework.Llm.Conversations.csproj --configuration Release
dotnet build ./src/Modules/CanDoItAll.Modules.LlmChats/CanDoItAll.Modules.LlmChats.csproj --configuration Release
dotnet build ./src/Modules/CanDoItAll.Modules.LlmChats.Persistence/CanDoItAll.Modules.LlmChats.Persistence.csproj --configuration Release
dotnet build ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release
```

Do not run all four after every edit. Build only the current owner and the nearest consumer when the
public contract changes.

## Unit filters

```powershell
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~LlmChatCanonicalModelTests"
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~LlmChatOperationIdempotencyTests"
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~LlmChatRuntimeFenceTests"
```

## PostgreSQL filters

Set `CANDOITALL_TESTS_POSTGRES_CONNECTION` according to repository test conventions.

```powershell
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~EfLlmConversationStoreIntegrationTests"
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~LlmChatPersistenceIntegrationTests"
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~LlmChatsDatabaseTransferIntegrationTests"
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~LlmChatsApiIntegrationTests"
```

## Migration

```powershell
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~MigrationBootstrapIntegrationTests"
dotnet ef migrations has-pending-model-changes --project ./src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --context AppDbContext
```

## Static bundle/architecture checks

```powershell
python ./codex/bundles/Simple-Llm-Chats-Backend-Api/scripts/validate_bundle.py --bundle-root ./codex/bundles/Simple-Llm-Chats-Backend-Api
python ./codex/bundles/Simple-Llm-Chats-Backend-Api/scripts/check_test_policy.py --bundle-root ./codex/bundles/Simple-Llm-Chats-Backend-Api
python ./codex/bundles/Simple-Llm-Chats-Backend-Api/scripts/check_architecture_boundaries.py --repo-root .
```

## Broad final gate

See `plan/04-test-budget-and-gates.md`. It is allowed only in SB11.
