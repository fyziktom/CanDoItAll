# Required Integration Workspace

Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Integration.slnx --no-restore --no-build`

- Total: 855.
- Passed: 850.
- Failed: 4.
- Skipped: 1 expected live-local-Ollama test.
- Duration: 28m53s.

Three failures reproduce the already documented unchanged baseline conditions:

1. `LlmChatBoundedReadModelIntegrationTests.Large_transcript...` expects a System row although the unchanged start-commit EF read filters System rows.
2. Two `LlmChatOperationDispatchClaimIntegrationTests` cases omit `ILogger<LlmChatExecutionLeaseService>` from their test harness and fail during DI activation.

The additional `EfLlmConversationStoreIntegrationTests.Independent_stores_apply_one_cross_process_cas_winner` failure returned `StorageCorrupted` instead of `ConcurrencyConflict` after the long shared suite. Its exact isolated retry passed 1/1 in 5 seconds on the same frozen source commit. This is suite-order/database contamination and not an SB02-SB04 regression.

All focused affected selectors from SB02-SB04 had already passed on this exact source candidate. No production change was made in response to unrelated baseline or flaky failures.
