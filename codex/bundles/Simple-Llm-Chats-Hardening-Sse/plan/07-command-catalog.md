# Focused command catalog

The executor must adjust class names to the implementation but preserve scope.

```powershell
# Affected builds
dotnet build ./src/Modules/CanDoItAll.Modules.LlmChats/CanDoItAll.Modules.LlmChats.csproj --configuration Release
dotnet build ./src/Modules/CanDoItAll.Modules.LlmChats.Persistence/CanDoItAll.Modules.LlmChats.Persistence.csproj --configuration Release
dotnet build ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release

# Focused unit families
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release `
  --filter "FullyQualifiedName~LlmChatOperation|FullyQualifiedName~LlmStreaming|FullyQualifiedName~LlmChatArchitecture"

# Focused PostgreSQL families
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj `
  --configuration Release --filter "FullyQualifiedName~LlmChatPersistence|FullyQualifiedName~LlmChatRuntimeFence|FullyQualifiedName~LlmChatDispatcher"

# Focused HTTP/SSE families
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj `
  --configuration Release --filter "FullyQualifiedName~LlmChatsApi|FullyQualifiedName~LlmChatSse"

# Migration model check, only after schema work
dotnet ef migrations has-pending-model-changes `
  --project ./src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj `
  --startup-project ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --context AppDbContext
```

Never copy a command into the execution report without its actual exit code, result count and current
commit SHA.
