# Current-head gates

All commands ran from `C:\repositories\CanDoItAll` with local sibling source projects.

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore -nologo -v:minimal -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmChat"` | 0 | 61 passed, 0 failed, 0 skipped; affected projects compiled with no reported warnings/errors |
| `dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -nologo -v:minimal -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmChatOperationEventJournalIntegrationTests\|FullyQualifiedName~LlmChatTurnTransactionIntegrationTests\|FullyQualifiedName~LlmChatsDatabaseTransferIntegrationTests"` | 0 | 7 passed, 0 failed, 0 skipped in 19 seconds; affected projects compiled with no reported warnings/errors |
| `$env:UseLocalCanDoItAllLibraries='true'; dotnet ef migrations has-pending-model-changes --no-build --project src\Foundation\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project src\App\CanDoItAll.Web\CanDoItAll.Web.csproj --context AppDbContext --configuration Debug` | 0 | No changes have been made to the model since the last migration |
| `git diff --check; git status --short; git rev-parse HEAD` | 0 | clean source head `e543e7bdd3de97e8f52db9d7df182f462b317742` before proof edits |

## Behavioral coverage

- eight independent PostgreSQL writers produce unique monotonic sequences;
- post-commit notification is observed only after commit and never after rollback;
- a new DbContext replays committed events without shared process memory;
- expired terminal journals are deleted while active journals survive;
- transcript completion and event evidence retain the existing transaction boundary;
- transfer schema v5 round-trips operation events;
- the stream coalescer preserves UTF-8 runes, flushes by time, and enforces aggregate/event bounds;
- failed partial output remains replay evidence, compensates the active turn, and creates no assistant
  transcript message;
- all current LLM Chat lease, cancellation, profile-fence, provider-audit, state-machine, and query unit
  tests remain green.
