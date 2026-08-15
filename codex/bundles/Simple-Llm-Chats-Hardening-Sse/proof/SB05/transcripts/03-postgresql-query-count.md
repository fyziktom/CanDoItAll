# PostgreSQL large-transcript and query-count proof

All commands ran from `C:\repositories\CanDoItAll` against ephemeral local PostgreSQL.

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.LlmChatBoundedReadModelIntegrationTests.Large_transcript_and_collection_reads_remain_keyset_bounded_with_constant_query_counts"` | 0 | 1 passed, 0 failed, 0 skipped |

The direct test seeds 24 definitions plus tags and one canonical transcript containing 2,000 messages
with an active pending turn. A `DbCommandInterceptor` proves:

- definition pages contain 10 rows, have stable next cursors, do not overlap, and execute 2 commands;
- the conversation list executes 1 command;
- a 25-message transcript page executes 2 commands and uses SQL `LIMIT`;
- provider-context resume executes 3 commands and returns exactly 12 messages, with the system entry
  first and exact pending user entry last;
- the persisted entry count remains 2,000, so the bounded reads do not mutate canonical content.
