# Red atomicity proof

Command:

```powershell
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --artifacts-path artifacts\codex\simple-llm-chats-hardening-sse\SB01-red -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmChatConversationTransactionIntegrationTests" /m:1 --logger "console;verbosity=minimal"
```

Result against the old implementation: exit code 1; 0 passed, 2 failed, 0 skipped.

- create failure: the independently committed transcript survived while the product row rolled back;
- rename failure: transcript title became `Renamed` while the product title rolled back to `Original`.

The tests inject failure after the store flush, which is the exact gap hidden by the old second
`AppDbContext` and nested transaction. Both failures therefore constitute semantic negative proof.
