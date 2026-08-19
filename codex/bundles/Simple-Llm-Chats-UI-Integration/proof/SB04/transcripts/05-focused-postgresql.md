# Focused production PostgreSQL validation

- Run label: `SB04-PG-PROJECTION-001`
- Working directory: `repo://`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Integration.slnx --no-restore -nologo -v:minimal --filter "FullyQualifiedName~EfLlmConversationStoreIntegrationTests.Conversation_read_projection_exposes_only_its_exact_active_operation_id"`
- Exit code: `0`

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

- Run label: `SB04-PG-LIFECYCLE-001`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Integration.slnx --no-restore -nologo -v:minimal --filter "FullyQualifiedName~LlmChatsApiPostgreSqlIntegrationTests.Profile_switch_before_finalization_retains_committed_usage_and_blocks_later_writes|FullyQualifiedName~LlmChatsApiPostgreSqlIntegrationTests.Request_lifetime_ends_before_provider_completion_and_does_not_cancel_durable_execution|FullyQualifiedName~LlmChatsApiPostgreSqlIntegrationTests.Full_retention_emits_gap_with_durable_high_water_then_closes_terminal|FullyQualifiedName~LlmChatsDatabaseTransferIntegrationTests.Transfer_rejects_invalid_operation_invocation_event_graph"`
- Exit code: `0`

```text
Passed: 4, Failed: 0, Skipped: 0, Total: 4, Duration: 25 s
```

The first run proves exact EF projection and cross-conversation isolation. The second proves real HTTP active identity, post-terminal omission, profile-fence lifetime, request-disconnect durability, replay gap/high-water behavior, one terminal close, and transfer graph rejection. Invariant IDs: `SB04-INV-01`, `SB04-INV-02`, `SB04-INV-03`, `SB04-INV-04`, `SB04-INV-05`.
