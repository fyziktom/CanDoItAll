# Required full Integration workspace

- Run label: `SB04-INTEGRATION-ALL-001`
- Working directory: `repo://`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Integration.slnx --no-restore -nologo -v:minimal`
- Exit code: `1`

```text
Passed: 851, Failed: 3, Skipped: 1, Total: 855, Duration: 29 m 5 s
Expected skip: LiveLocalOllamaThinkingEffortIntegrationTests.Installed_catalog_and_native_effort_mapping_match_thinking_capabilities
Failure 1: LlmChatBoundedReadModelIntegrationTests.Large_transcript_and_collection_reads_remain_keyset_bounded_with_constant_query_counts
  Expected seeded System entry first, but the unchanged query filters Role == System before paging.
Failure 2: LlmChatOperationDispatchClaimIntegrationTests.Independent_postgresql_services_admit_once_and_claim_execution_lease_once
  DI cannot resolve ILogger<LlmChatExecutionLeaseService> in the existing test harness.
Failure 3: LlmChatOperationDispatchClaimIntegrationTests.Remote_cancellation_is_observed_by_the_current_owner_heartbeat
  DI cannot resolve ILogger<LlmChatExecutionLeaseService> in the existing test harness.
```

- Run label: `SB04-INTEGRATION-BASELINE-REPRO-001`
- Command: isolated selector for the bounded-read test and dispatch-claim class
- Exit code: `1`

```text
Passed: 0, Failed: 3, Skipped: 0, Total: 3, Duration: 10 s
The same assertion and two pre-activation DI failures reproduced exactly.
```

The three failures are outside the candidate diff and do not execute the SB04 contract. At start commit `a0732674a859bf46a76d49efd245dd91681575fd`, `EfLlmChatConversationReadStore.TryGetTranscriptPageAsync` already excludes System rows, and the dispatch harness already lacks logging registration. All changed-behavior integration selectors pass in `bundle://proof/SB04/transcripts/04-focused-api.md` and `bundle://proof/SB04/transcripts/05-focused-postgresql.md`. Invariant IDs: `SB04-INV-01` through `SB04-INV-05`.
