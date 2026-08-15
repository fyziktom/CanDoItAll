# Final PostgreSQL proof

Command:

```powershell
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --artifacts-path artifacts\codex\simple-llm-chats-hardening-sse\SB01-red -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~EfLlmConversationStoreIntegrationTests|FullyQualifiedName~LlmChatConversationTransactionIntegrationTests|FullyQualifiedName~LlmChatCanonicalTitleMigrationIntegrationTests|FullyQualifiedName~LlmChatsDatabaseTransferIntegrationTests" /m:1 --logger "console;verbosity=minimal"
```

Final result: exit code 0; 7 passed, 0 failed, 0 skipped; 19 seconds.

The slice covers cross-context CAS, exact compensation, create/rename rollback, matching legacy
migration, divergent-title rejection, and schema-version-2 database transfer.

Diagnostic history: after the architecture gate removed the remaining duplicate transcript timestamp
columns, one focused run failed two migration cases because the previous-schema test insert omitted
now-unmapped non-null columns. The helper was corrected to install and remove temporary legacy-column
defaults. The unchanged product migration then passed the final seven-case run above.
